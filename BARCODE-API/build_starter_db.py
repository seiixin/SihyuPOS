import sys
import io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8', errors='replace')

import urllib.request
import urllib.parse
import json
import sqlite3
import time
import os

OUTPUT_JSON = os.path.join(os.path.dirname(os.path.abspath(__file__)), "ph_grocery_starter.json")
OUTPUT_SQLITE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "starter_db.sqlite")
PAGE_SIZE = 100
MAX_PAGES = 30

def fetch_ph_products():
    print("[START] Starting Open Food Facts Philippine Dataset Scraper...\n")
    
    all_products = []
    barcode_set = set()

    for page in range(1, MAX_PAGES + 1):
        print(f"fetching Page {page} of {MAX_PAGES}...")

        params = urllib.parse.urlencode({
            "action": "process",
            "tagtype_0": "countries",
            "tag_contains_0": "contains",
            "tag_0": "philippines",
            "page_size": PAGE_SIZE,
            "page": page,
            "json": "true",
            "fields": "code,product_name,brands"
        })
        url = f"https://world.openfoodfacts.org/cgi/search.pl?{params}"

        req = urllib.request.Request(url, headers={
            "User-Agent": "PH-POS-StarterPackBuilder/1.0 (offline-pos-dev)"
        })

        try:
            with urllib.request.urlopen(req, timeout=30) as resp:
                if resp.status != 200:
                    print(f"[WARN] Page {page} returned status {resp.status}. Skipping...")
                    continue

                data = json.loads(resp.read().decode("utf-8"))
                products = data.get("products", [])

                if len(products) == 0:
                    print("[INFO] No more products found. Stopping fetch loop.")
                    break

                for item in products:
                    barcode = (item.get("code") or "").strip()
                    product_name = (item.get("product_name") or "").strip()
                    brand = (item.get("brands") or "").strip()

                    if barcode and product_name:
                        if barcode not in barcode_set:
                            barcode_set.add(barcode)
                            all_products.append({
                                "barcode": barcode,
                                "product_name": product_name,
                                "brand": brand
                            })

                time.sleep(0.3)

        except Exception as err:
            print(f"[ERROR] fetching page {page}: {err}")

    print(f"\n[DONE] Total unique Philippine products scraped: {len(all_products)}")

    with open(OUTPUT_JSON, "w", encoding="utf-8") as f:
        json.dump(all_products, f, indent=2, ensure_ascii=False)
    print(f"[SAVE] Saved JSON to: {OUTPUT_JSON}")

    db = sqlite3.connect(OUTPUT_SQLITE)
    cur = db.cursor()
    cur.executescript("""
        CREATE TABLE IF NOT EXISTS products (
            barcode TEXT PRIMARY KEY,
            product_name TEXT NOT NULL,
            brand TEXT
        );
        CREATE INDEX IF NOT EXISTS idx_barcode ON products(barcode);
    """)
    cur.executemany(
        "INSERT OR REPLACE INTO products (barcode, product_name, brand) VALUES (?, ?, ?)",
        [(p["barcode"], p["product_name"], p["brand"]) for p in all_products]
    )
    db.commit()
    db.close()
    print(f"[SAVE] Saved SQLite DB to: {OUTPUT_SQLITE}")
    print("\n[SUCCESS] Done! Ready to embed in your Offline POS system.")

if __name__ == "__main__":
    fetch_ph_products()
