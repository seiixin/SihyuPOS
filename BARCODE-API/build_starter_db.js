const fs = require('fs');
const path = require('path');
const Database = require('better-sqlite3');

// Configuration
const OUTPUT_JSON = path.join(__dirname, 'ph_grocery_starter.json');
const OUTPUT_SQLITE = path.join(__dirname, 'starter_db.sqlite');
const PAGE_SIZE = 100; // Maximum allowed per page by OFF
const MAX_PAGES = 30;  // 30 pages * 100 = ~3,000 items

async function fetchPHProducts() {
  console.log('🚀 Starting Open Food Facts Philippine Dataset Scraper...\n');
  
  let allProducts = [];
  let barcodeSet = new Set(); // To prevent duplicate barcodes

  for (let page = 1; page <= MAX_PAGES; page++) {
    console.log(`fetching Page ${page} of ${MAX_PAGES}...`);
    
    // Search products with country=philippines or barcode prefix starting with 480
    const url = `https://world.openfoodfacts.org/cgi/search.pl?action=process&tagtype_0=countries&tag_contains_0=contains&tag_0=philippines&page_size=${PAGE_SIZE}&page=${page}&json=true&fields=code,product_name,brands`;

    try {
      const response = await fetch(url, {
        headers: { 'User-Agent': 'PH-POS-StarterPackBuilder/1.0 (offline-pos-dev)' }
      });

      if (!response.ok) {
        console.warn(`⚠️ Warning: Page ${page} returned status ${response.status}. Skipping...`);
        continue;
      }

      const data = await response.json();
      const products = data.products || [];

      if (products.length === 0) {
        console.log('ℹ️ No more products found. Stopping fetch loop.');
        break;
      }

      for (const item of products) {
        const barcode = item.code?.trim();
        const productName = item.product_name?.trim();
        const brand = item.brands?.trim() || '';

        // Filter: Must have a barcode and product name
        if (barcode && productName) {
          // Extra check: Accept 480 prefix or any valid code from PH search
          if (!barcodeSet.has(barcode)) {
            barcodeSet.add(barcode);
            allProducts.push({
              barcode: barcode,
              product_name: productName,
              brand: brand
            });
          }
        }
      }

      // Small delay to be respectful to OFF servers
      await new Promise(resolve => setTimeout(resolve, 300));

    } catch (err) {
      console.error(`❌ Error fetching page ${page}:`, err.message);
    }
  }

  console.log(`\n✅ Total unique Philippine products scraped: ${allProducts.length}`);

  // 1. Save to JSON File
  fs.writeFileSync(OUTPUT_JSON, JSON.stringify(allProducts, null, 2), 'utf-8');
  console.log(`💾 Saved JSON to: ${OUTPUT_JSON}`);

  // 2. Save to SQLite Database
  const db = new Database(OUTPUT_SQLITE);
  
  // Create Table
  db.exec(`
    CREATE TABLE IF NOT EXISTS products (
      barcode TEXT PRIMARY KEY,
      product_name TEXT NOT NULL,
      brand TEXT
    );
    CREATE INDEX IF NOT EXISTS idx_barcode ON products(barcode);
  `);

  // Insert items using Transaction (Fast batch insertion)
  const insertStmt = db.prepare(`
    INSERT OR REPLACE INTO products (barcode, product_name, brand) 
    VALUES (@barcode, @product_name, @brand)
  `);

  const insertMany = db.transaction((items) => {
    for (const item of items) insertStmt.run(item);
  });

  insertMany(allProducts);
  db.close();

  console.log(`💾 Saved SQLite DB to: ${OUTPUT_SQLITE}`);
  console.log('\n🎉 Done! Ready to embed in your Offline POS system.');
}

fetchPHProducts();
