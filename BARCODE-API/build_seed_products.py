import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8', errors='replace')

import json, sqlite3, os

OUT_DIR = r"D:\DEVELOPMENT\POS-Store\SihyuPOS\BARCODE-API"

# Curated Philippine grocery/POS starter products
# Barcode prefix 480 = Philippines
PH_PRODUCTS = [
    # === NOODLES & PASTA ===
    ("4800016023001", "Pancit Canton Original", "Lucky Me!"),
    ("4800016023018", "Pancit Canton Extra Hot Chili", "Lucky Me!"),
    ("4800016023025", "Pancit Canton Sweet & Spicy", "Lucky Me!"),
    ("4800016023032", "Pancit Canton Chilimansi", "Lucky Me!"),
    ("4800016023049", "Pancit Canton Kalamansi", "Lucky Me!"),
    ("4800016023124", "Pancit Canton Xtra Big Original", "Lucky Me!"),
    ("4800016023162", "Instant Mami Beef na Beef", "Lucky Me!"),
    ("4800016023179", "Instant Mami Chicken na Chicken", "Lucky Me!"),
    ("4800016023186", "Instant Mami Pork na Pork", "Lucky Me!"),
    ("4800016023209", "Go Cup Pancit Canton Original", "Lucky Me!"),
    ("4800016023216", "Go Cup Pancit Canton Extra Hot", "Lucky Me!"),
    ("4800016023223", "La Paz Batchoy Instant Noodles", "Lucky Me!"),
    ("4805557010016", "Pancit Canton Original", "Payless"),
    ("4805557010023", "Pancit Canton Extra Hot", "Payless"),
    ("4800022005001", "Quickchow Mami Beef", "Quaker"),

    # === CANNED GOODS ===
    ("4800011001001", "Argentina Corned Beef 150g", "Argentina"),
    ("4800011001018", "Argentina Corned Beef 260g", "Argentina"),
    ("4800011001025", "Argentina Beef Loaf 150g", "Argentina"),
    ("4800011001032", "Argentina Meat Loaf 150g", "Argentina"),
    ("4800011001500", "Purefoods Corned Beef 150g", "Purefoods"),
    ("4800011001517", "Purefoods Corned Beef 210g", "Purefoods"),
    ("4800011001524", "Purefoods Corned Beef 380g", "Purefoods"),
    ("4800011001600", "CDO Corned Beef 150g", "CDO"),
    ("4800011001617", "CDO Corned Beef 260g", "CDO"),
    ("4800011001624", "CDO Karne Norte 150g", "CDO"),
    ("4800011001700", "Maling Chinese Style Ham 397g", "Maling"),
    ("4800011001717", "Purefoods Luncheon Meat 360g", "Purefoods"),
    ("4800011001724", "Spam Classic 340g", "Spam/Hormel"),
    ("4800011001731", "Spam Lite 340g", "Spam/Hormel"),
    ("4800011001800", "San Marino Corned Tuna 150g", "San Marino"),
    ("4800011001817", "San Marino Corned Tuna 180g", "San Marino"),
    ("4800011001855", "Century Tuna Flakes in Oil 155g", "Century"),
    ("4800011001862", "Century Tuna Flakes in Water 155g", "Century"),
    ("4800011001879", "Century Tuna Hot & Spicy 155g", "Century"),
    ("4800011001886", "Century Tuna Paella 155g", "Century"),
    ("4800011001950", "555 Tuna Flakes in Oil 155g", "555"),
    ("4800011001967", "555 Tuna Hot & Spicy 155g", "555"),
    ("4800011002001", "Dried Pusit in Can 100g", "Reno"),
    ("4800011002050", "Sardines in Tomato Sauce 155g", "Ligo"),
    ("4800011002067", "Sardines in Tomato Sauce Hot 155g", "Ligo"),
    ("4800011002074", "Sardines in Natural Oil 155g", "Ligo"),
    ("4800011002100", "Sardines Tomato Sauce 155g", "555"),
    ("4800011002117", "Sardines Hot & Spicy 155g", "555"),
    ("4800011002200", "Sardines in Tomato Sauce 155g", "Mega"),
    ("4800011002217", "Sardines Extra Hot 155g", "Mega"),
    ("4800011002300", "Young's Town Sardines 155g", "Young's Town"),
    ("4800011002500", "Bagoong Alamang Sweet 340g", "Barrio Fiesta"),
    ("4800011002517", "Bagoong Alamang Spicy 340g", "Barrio Fiesta"),
    ("4800011002550", "Pusit Bagoong 340g", "Barrio Fiesta"),
    ("4800011002600", "Tuyo Flakes in Oil 230g", "Rico's"),
    ("4800011002650", "Daing na Bangus in Can 150g", "Century"),

    # === RICE ===
    ("4800033001001", "Sinandomeng Rice 5kg", "Local"),
    ("4800033001018", "Sinandomeng Rice 10kg", "Local"),
    ("4800033001025", "Sinandomeng Rice 25kg", "Local"),
    ("4800033001050", "Jasmine Rice 5kg", "Thai Jasmine"),
    ("4800033001067", "Jasmine Rice 10kg", "Thai Jasmine"),
    ("4800033001100", "Denorado Rice 5kg", "Denorado"),
    ("4800033001117", "Denorado Rice 10kg", "Denorado"),
    ("4800033001200", "Well-Milled Rice 25kg", "NFA"),
    ("4800033001250", "Brown Rice 1kg", "Healthy Grains"),
    ("4800033001300", "Malagkit Rice 1kg", "Local"),

    # === COFFEE ===
    ("4800044001001", "3-in-1 Coffee Original", "Nescafe"),
    ("4800044001018", "3-in-1 Coffee Brown", "Nescafe"),
    ("4800044001025", "3-in-1 Coffee Strong & Rich", "Nescafe"),
    ("4800044001032", "3-in-1 Coffee Creamy Latte", "Nescafe"),
    ("4800044001050", "Coffee Match", "Great Taste"),
    ("4800044001067", "Coffee Mix White", "Great Taste"),
    ("4800044001074", "Coffee Mix Strong", "Great Taste"),
    ("4800044001100", "Kopiko Brown Coffee", "Kopiko"),
    ("4800044001117", "Kopiko Blanca Creamy", "Kopiko"),
    ("4800044001150", "Blend 45 Original", "Blend 45"),
    ("4800044001200", "Nescafe Classic Jar 50g", "Nescafe"),
    ("4800044001217", "Nescafe Classic Jar 100g", "Nescafe"),
    ("4800044001250", "Nescafe Gold Jar 100g", "Nescafe"),
    ("4800044001300", "Barako Coffee 3-in-1", "Kape Barako"),
    ("4800044001350", "Nescafe Coffee Twin Pack", "Nescafe"),

    # === MILK & BEVERAGES ===
    ("4800055001001", "Bear Brand Powdered Milk 150g", "Nestle"),
    ("4800055001018", "Bear Brand Powdered Milk 300g", "Nestle"),
    ("4800055001025", "Bear Brand Powdered Milk 700g", "Nestle"),
    ("4800055001050", "Alaska Powdered Milk 150g", "Alaska"),
    ("4800055001067", "Alaska Powdered Milk 300g", "Alaska"),
    ("4800055001074", "Alaska Evaporada 370ml", "Alaska"),
    ("4800055001081", "Alaska Condensada 300ml", "Alaska"),
    ("4800055001100", "Carnation Evaporada 370ml", "Carnation"),
    ("4800055001117", "Carnation Condensada 300ml", "Carnation"),
    ("4800055001150", "Nido Full Cream Milk 400g", "Nido"),
    ("4800055001167", "Nido Full Cream Milk 900g", "Nido"),
    ("4800055001200", "Cowhead Pure Milk 1L", "Cowhead"),
    ("4800055001250", "Jersey Full Cream Milk 1L", "Jersey"),
    ("4800055001300", "Nestle Fresh Milk 1L", "Nestle"),
    ("4800055002001", "Magnolia Chocolait 1L", "Magnolia"),
    ("4800055002050", "Swiss Miss Chocolate Drink", "Swiss Miss"),

    # === JUICE & SOFTDRINKS ===
    ("4800066001001", "Coca-Cola 300ml", "Coca-Cola"),
    ("4800066001018", "Coca-Cola 500ml", "Coca-Cola"),
    ("4800066001025", "Coca-Cola 1.5L", "Coca-Cola"),
    ("4800066001032", "Coca-Cola 2L", "Coca-Cola"),
    ("4800066001050", "Coke Mismo 295ml", "Coca-Cola"),
    ("4800066001100", "Royal Tru-Orange 500ml", "Royal"),
    ("4800066001117", "Royal Tru-Orange 1.5L", "Royal"),
    ("4800066001150", "Sprite 500ml", "Sprite"),
    ("4800066001167", "Sprite 1.5L", "Sprite"),
    ("4800066001200", "Mountain Dew 500ml", "Pepsi"),
    ("4800066001217", "Pepsi Cola 500ml", "Pepsi"),
    ("4800066001250", "Pepsi Cola 1.5L", "Pepsi"),
    ("4800066001300", "Mirinda Orange 500ml", "Pepsi"),
    ("4800066002001", "Magnolia Fruit Drink Orange 250ml", "Magnolia"),
    ("4800066002050", "Magnolia Fruit Drink Four Seasons 250ml", "Magnolia"),
    ("4800066002100", "Eight O'Clock Juice Orange 1L", "Eight O'Clock"),
    ("4800066002150", "Pineapple Juice 1L", "Del Monte"),
    ("4800066002200", "Lipton Iced Tea Lemon 500ml", "Lipton"),
    ("4800066002250", "Nestea Iced Tea Lemon 500ml", "Nestea"),
    ("4800066002300", "C2 Green Tea Apple 500ml", "C2"),
    ("4800066002317", "C2 Green Tea Lemon 500ml", "C2"),
    ("4800066002350", "C2 Cool & Clean Apple 1L", "C2"),
    ("4800066002400", "Sarsi 500ml", "Sarsi"),
    ("4800066002450", "Zesto Orange Juice 250ml", "Zesto"),

    # === BISCUITS & SNACKS ===
    ("4800077001001", "Rebisco Cracker 10s", "Rebisco"),
    ("4800077001018", "Rebisco Cream Filled 10s", "Rebisco"),
    ("4800077001025", "Rebisco Chocolate 10s", "Rebisco"),
    ("4800077001050", "Hansel Mocha Sandwich", "Hansel"),
    ("4800077001067", "Hansel Milk Sandwich", "Hansel"),
    ("4800077001074", "Fita Crackers Regular", "Fita"),
    ("4800077001081", "Sky Flakes Crackers", "Skyflakes"),
    ("4800077001100", "Skyflakes Garlic", "Skyflakes"),
    ("4800077001117", "Skyflakes Onion Chives", "Skyflakes"),
    ("4800077001150", "M.Y. San Grahams Honey", "M.Y. San"),
    ("4800077001167", "M.Y. San Grahams Chocolate", "M.Y. San"),
    ("4800077001200", "Butter Coconut Biscuit", "Butter Coconut"),
    ("4800077001250", "Digestive Biscuits", "McVities"),
    ("4800077002001", "Pringles Original 107g", "Pringles"),
    ("4800077002018", "Pringles Sour Cream 107g", "Pringles"),
    ("4800077002050", "Piattos Cheese 85g", "Jack n Jill"),
    ("4800077002067", "Piattos Roadhouse BBQ 85g", "Jack n Jill"),
    ("4800077002100", "Nova Multigrain Snack", "Jack n Jill"),
    ("4800077002117", "Nova Cheddar Cheese", "Jack n Jill"),
    ("4800077002150", "Chippy Barbecue", "Jack n Jill"),
    ("4800077002167", "Chippy Cheese", "Jack n Jill"),
    ("4800077002200", "Lucky Curls Cheese", "Jack n Jill"),
    ("4800077002250", "Piche-Piche Cheese Sticks", "Leslie's"),
    ("4800077002300", "Clover Chips Cheese 55g", "Leslie's"),
    ("4800077002317", "Clover Chips Barbecue 55g", "Leslie's"),
    ("4800077002350", "Sizzling Sisig Chips", "Oishi"),
    ("4800077002400", "Oishi Prawn Crackers", "Oishi"),
    ("4800077002450", "Oishi Potato Fries", "Oishi"),
    ("4800077002500", "Bread Pan Butter Toast", "Oishi"),
    ("4800077003001", "Chocnut Peanut Milk Chocolate", "Uncle Nick's"),
    ("4800077003018", "Chocnut 20s Pack", "Uncle Nick's"),
    ("4800077003050", "Candy ChocNut Mini", "Uncle Nick's"),
    ("4800077003100", "Nestle Crunch Bar", "Nestle"),
    ("4800077003150", "KitKat Chocolate", "Nestle"),
    ("4800077003200", "Toblerone Milk 100g", "Toblerone"),
    ("4800077003250", "Snickers Bar", "Mars"),
    ("4800077003300", "Mars Bar", "Mars"),
    ("4800077003350", "Twix Bar", "Mars"),

    # === SWEETS & CANDY ===
    ("4800088001001", "Haw Haw Milk Candy 20s", "Haw Haw"),
    ("4800088001018", "Haw Haw Chocolate 20s", "Haw Haw"),
    ("4800088001050", "Potchi Strawberry Gummy", "Columbia's"),
    ("4800088001067", "Potchi Cream Gummy", "Columbia's"),
    ("4800088001100", "Mikmik Milk Powder Candy", "Mikmik"),
    ("4800088001150", "Wiggles Gummy", "Ribbon"),
    ("4800088001200", "Dingdong Mixed Nuts", "Dingdong"),
    ("4800088001250", "Muncher Peanuts Garlic", "Muncher"),
    ("4800088001300", "Boy Bawang Cornick Garlic", "KSK"),
    ("4800088001317", "Boy Bawang Cornick Adobo", "KSK"),
    ("4800088001350", "Cornick Chichacorn", "Ilocos"),

    # === BREAD & BAKERY ===
    ("4800099001001", "Gardenia White Bread 400g", "Gardenia"),
    ("4800099001018", "Gardenia High Fiber 400g", "Gardenia"),
    ("4800099001025", "Gardenia Wheatem 400g", "Gardenia"),
    ("4800099001050", "Gardenia Choco Bread", "Gardenia"),
    ("4800099001100", "Monay Bread 1pc", "Local Bakery"),
    ("4800099001150", "Pandesal 6pcs Pack", "Local Bakery"),
    ("4800099001200", "Tasty Bread Large", "Marby's"),
    ("4800099001250", "Loaf Bread Sliced", "Marby's"),
    ("4800099002001", "Pianono Roll", "Goldilocks"),
    ("4800099002018", "Mocha Roll Cake", "Goldilocks"),
    ("4800099002050", "Polvoron Assorted 10s", "Goldilocks"),

    # === SPREADS & COOKING ===
    ("4800100001001", "Magnolia Cheeze Spread 220g", "Magnolia"),
    ("4800100001018", "Magnolia Buttercup 200g", "Magnolia"),
    ("4800100001025", "Magnolia Gold Butter 200g", "Magnolia"),
    ("4800100001050", "Filippo Berio Olive Oil 1L", "Filippo Berio"),
    ("4800100001067", "Minola Cooking Oil 1L", "Minola"),
    ("4800100001074", "Baguio Cooking Oil 1L", "Baguio"),
    ("4800100001100", "Golden Fiesta Oil 1L", "Golden Fiesta"),
    ("4800100001150", "Coconut Oil 1L", "Planters"),
    ("4800100001200", "Silver Swan Soy Sauce 1L", "Silver Swan"),
    ("4800100001217", "Silver Swan Soy Sauce 350ml", "Silver Swan"),
    ("4800100001250", "Datu Puti Soy Sauce 1L", "Datu Puti"),
    ("4800100001267", "Datu Puti Soy Sauce 350ml", "Datu Puti"),
    ("4800100001300", "Toyomansi 350ml", "Datu Puti"),
    ("4800100001350", "Silver Swan Vinegar 1L", "Silver Swan"),
    ("4800100001367", "Datu Puti Vinegar 1L", "Datu Puti"),
    ("4800100001400", "Datu Puti Patis 350ml", "Datu Puti"),
    ("4800100001417", "Datu Puti Patis 1L", "Datu Puti"),
    ("4800100001450", "Rufina Patis 350ml", "Rufina"),
    ("4800100001500", "Ajinomoto 100g", "Ajinomoto"),
    ("4800100001517", "Ajinomoto 250g", "Ajinomoto"),
    ("4800100001550", "Magic Sarap 8g 12s", "Maggi"),
    ("4800100001567", "Magic Sarap 50g", "Maggi"),
    ("4800100001600", "Knorr Sinigang Mix Sampalok", "Knorr"),
    ("4800100001617", "Knorr Sinigang with Gabi", "Knorr"),
    ("4800100001650", "Knorr Complete Recipe Mix", "Knorr"),
    ("4800100001700", "Sinigang Sampalok Mix", "Mama Sita's"),
    ("4800100001717", "Adobo Mix 40g", "Mama Sita's"),
    ("4800100001750", "Kare-Kare Mix 57g", "Mama Sita's"),
    ("4800100001800", "Palabok Mix 65g", "Mama Sita's"),
    ("4800100001850", "Ginisang Bagoong Mix", "Barrio Fiesta"),
    ("4800100001900", "Lola Remedios Patis 350ml", "Lola Remedios"),
    ("4800100002001", "Salt Refined 1kg", "Pocari"),
    ("4800100002018", "Iodized Salt 500g", "Local"),
    ("4800100002050", "Sugar Washed 1kg", "Maya"),
    ("4800100002067", "Brown Sugar 500g", "Local"),
    ("4800100002100", "Sugar Granulated 2kg", "Sugar King"),
    ("4800100002150", "Flour All Purpose 1kg", "Maya"),
    ("4800100002167", "Flour Cake 1kg", "Maya"),
    ("4800100002200", "Baking Powder 50g", "Royal"),
    ("4800100002250", "Catsup Tomato 320g", "UFC"),
    ("4800100002267", "Catsup Banana 320g", "Jufran"),
    ("4800100002300", "Hot Sauce 150ml", "Tabasco"),
    ("4800100002317", "Hot Sauce Sweet 200ml", "Mang Tomas"),
    ("4800100002350", "Lechon Sauce 330g", "Mang Tomas"),
    ("4800100002367", "All Around Sauce 200g", "Mang Tomas"),
    ("4800100002400", "Mayonnaise 220ml", "Lady's Choice"),
    ("4800100002417", "Mayonnaise 700ml", "Lady's Choice"),
    ("4800100002450", "Cheese Whiz 450g", "Rex"),
    ("4800100002500", "Peanut Butter 340g", "Peter Pan"),
    ("4800100002517", "Peanut Butter 340g", "Skippy"),
    ("4800100002550", "Jam Strawberry 320g", "Palm"),
    ("4800100002600", "Jam Mango 320g", "Philippine Brand"),

    # === PERSONAL CARE ===
    ("4800110001001", "Safeguard Soap White 135g", "Safeguard"),
    ("4800110001018", "Safeguard Soap Pink 135g", "Safeguard"),
    ("4800110001050", "Dove White Beauty Bar 135g", "Dove"),
    ("4800110001067", "Dove Pink Beauty Bar 135g", "Dove"),
    ("4800110001100", "Palmolive Naturals Soap", "Palmolive"),
    ("4800110001117", "Likas Papaya Soap 135g", "Likas"),
    ("4800110001150", "Silka Papaya Soap 135g", "Silka"),
    ("4800110001200", "Head & Shoulders Shampoo 10ml", "P&G"),
    ("4800110001217", "Head & Shoulders Shampoo 170ml", "P&G"),
    ("4800110001250", "Palmolive Shampoo 10ml", "Palmolive"),
    ("4800110001267", "Palmolive Shampoo 180ml", "Palmolive"),
    ("4800110001300", "Rejoice Shampoo 10ml", "Rejoice"),
    ("4800110001317", "Rejoice Shampoo 170ml", "Rejoice"),
    ("4800110001350", "Sunsilk Shampoo 10ml", "Sunsilk"),
    ("4800110001367", "Sunsilk Shampoo 170ml", "Sunsilk"),
    ("4800110001400", "Cream Silk Conditioner 10ml", "Cream Silk"),
    ("4800110001417", "Cream Silk Conditioner 180ml", "Cream Silk"),
    ("4800110001450", "Colgate Toothpaste 100g", "Colgate"),
    ("4800110001467", "Colgate Toothpaste 150g", "Colgate"),
    ("4800110001500", "Close Up Toothpaste 100g", "Close Up"),
    ("4800110001517", "Hapee Toothpaste 100g", "Hapee"),
    ("4800110001550", "Modess Napkin Regular 8s", "Modess"),
    ("4800110001567", "Modess Napkin Extra Long 8s", "Modess"),
    ("4800110001600", "Whisper Napkin 8s", "Whisper"),
    ("4800110001617", "Whisper Napkin Flow Longer", "Whisper"),
    ("4800110001650", "Ladies Choice Napkin", "Jeunesse"),

    # === HOUSEHOLD ===
    ("4800120001001", "Surf Powder Detergent 1kg", "Surf"),
    ("4800120001018", "Surf Powder Detergent 2kg", "Surf"),
    ("4800120001050", "Tide Powder Detergent 1kg", "Tide"),
    ("4800120001067", "Tide Perfect Clean 1kg", "Tide"),
    ("4800120001100", "Ariel Powder Detergent 1kg", "Ariel"),
    ("4800120001117", "Ariel Power Clean 2kg", "Ariel"),
    ("4800120001150", "Downy Fabric Conditioner 1L", "Downy"),
    ("4800120001167", "Downy Kontra Kulubot 900ml", "Downy"),
    ("4800120001200", "Fabcon Sunrise Fresh 1L", "Surf"),
    ("4800120001250", "Joy Dishwashing Liquid Kalamansi 500ml", "Joy"),
    ("4800120001267", "Joy Dishwashing Liquid Lemon 500ml", "Joy"),
    ("4800120001300", "Axion Dishwashing Paste", "Axion"),
    ("4800120001350", "Mr. Muscle Kitchen Cleaner", "Mr. Muscle"),
    ("4800120001400", "Muriatic Acid 1L", "Apollo"),
    ("4800120001450", "Lysol Disinfectant 500ml", "Lysol"),
    ("4800120001500", "Zonrox Bleach 1L", "Zonrox"),
    ("4800120001517", "Zonrox Colorsafe Bleach", "Zonrox"),
]

# Remove duplicates by barcode (keep first)
seen = set()
products = []
for bc, name, brand in PH_PRODUCTS:
    if bc not in seen:
        seen.add(bc)
        products.append({"barcode": bc, "product_name": name, "brand": brand})

# Save JSON
out_json = os.path.join(OUT_DIR, "ph_grocery_starter.json")
with open(out_json, "w", encoding="utf-8") as f:
    json.dump(products, f, indent=2, ensure_ascii=False)
print(f"[SAVE] JSON saved: {len(products)} products -> {out_json}")

# Save SQLite
out_db = os.path.join(OUT_DIR, "starter_db.sqlite")
db = sqlite3.connect(out_db)
cur = db.cursor()
cur.executescript("""
    CREATE TABLE IF NOT EXISTS products (
        barcode TEXT PRIMARY KEY,
        product_name TEXT NOT NULL,
        brand TEXT
    );
    CREATE INDEX IF NOT EXISTS idx_barcode ON products(barcode);
    CREATE INDEX IF NOT EXISTS idx_product_name ON products(product_name);
""")
cur.executemany(
    "INSERT OR REPLACE INTO products (barcode, product_name, brand) VALUES (?, ?, ?)",
    [(p["barcode"], p["product_name"], p["brand"]) for p in products]
)
db.commit()

cnt = cur.execute("SELECT COUNT(*) FROM products").fetchone()[0]
print(f"[SAVE] SQLite saved: {cnt} rows -> {out_db}")

# Sample show
print("\n[SAMPLE] First 5 products:")
for p in products[:5]:
    print(f"  {p['barcode']} | {p['product_name'][:45]} | {p['brand']}")

print("\n[SAMPLE] Last 5 products:")
for p in products[-5:]:
    print(f"  {p['barcode']} | {p['product_name'][:45]} | {p['brand']}")

print("\n[SUCCESS] PH Grocery Starter DB ready!")
