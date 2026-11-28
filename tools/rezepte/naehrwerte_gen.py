# Erzeugt die Nährwert-Tabelle für T-0787 aus USDA FoodData Central SR Legacy (CC0 1.0).
# Aufruf: python -I naehrwerte_gen.py <csv-ordner> <ziel.cs> <ziel-herkunft.tsv>
import csv, re, sys

src, out_cs, out_tsv = sys.argv[1], sys.argv[2], sys.argv[3]

# (kennung, deutscher Name, Suchwörter (|-getrennt), Muster in der USDA-Beschreibung, Gramm je Stück oder 0)
MAP = [
    ("ei", "Ei (Hühnerei)", "eier|hühnerei|eigelb ganz", r"^Egg, whole, raw, fresh$", 50),
    ("eigelb", "Eigelb", "dotter", r"^Egg, yolk, raw, fresh$", 0),
    ("eiweiss", "Eiweiß (Eiklar)", "eiklar|eischnee", r"^Egg, white, raw, fresh$", 0),
    ("weizenmehl", "Weizenmehl (Type 405/550)", "mehl|weißmehl|type 405|type 550", r"^Wheat flour, white, all-purpose, enriched, unbleached$", 0),
    ("vollkornmehl", "Weizenvollkornmehl", "vollkornmehl|type 1600", r"^Wheat flour, whole-grain \(", 0),
    ("brotmehl", "Weizenmehl Type 812 (Brotmehl)", "type 812|brotmehl", r"^Wheat flour, white, bread, enriched$", 0),
    ("roggenmehl", "Roggenmehl", "roggen", r"^Rye flour, medium$", 0),
    ("dinkel", "Dinkel (Korn)", "dinkelkorn", r"^Spelt, uncooked$", 0),
    ("griess", "Hartweizengrieß", "grieß|semola", r"^Semolina, unenriched$", 0),
    ("speisestaerke", "Speisestärke (Mais)", "stärke|maisstärke", r"^Cornstarch$", 0),
    ("backpulver", "Backpulver", "", r"^Leavening agents, baking powder, double-acting, sodium aluminum sulfate$", 0),
    ("hefe-frisch", "Hefe, frisch", "frischhefe|hefe", r"^Leavening agents, yeast, baker's, compressed$", 0),
    ("hefe-trocken", "Trockenhefe", "", r"^Leavening agents, yeast, baker's, active dry$", 0),
    ("zucker", "Zucker", "kristallzucker|haushaltszucker", r"^Sugars, granulated$", 0),
    ("brauner-zucker", "Brauner Zucker", "rohrzucker", r"^Sugars, brown$", 0),
    ("puderzucker", "Puderzucker", "", r"^Sugars, powdered$", 0),
    ("honig", "Honig", "", r"^Honey$", 0),
    ("ahornsirup", "Ahornsirup", "sirup", r"^Syrups, maple$", 0),
    ("marmelade", "Konfitüre", "marmelade|gelee", r"^Jams and preserves$", 0),
    ("kakao", "Kakaopulver (ungesüßt)", "kakao", r"^Cocoa, dry powder, unsweetened$", 0),
    ("zartbitter", "Zartbitterschokolade (70–85 %)", "schokolade|bitterschokolade", r"^Chocolate, dark, 70-85% cacao solids$", 0),
    ("salz", "Salz", "speisesalz|kochsalz", r"^Salt, table$", 0),
    ("butter", "Butter", "süßrahmbutter", r"^Butter, without salt$", 0),
    ("margarine", "Margarine", "", r"^Margarine, regular, 80% fat, composite, stick, with salt", 0),
    ("olivenoel", "Olivenöl", "öl|olivenöl", r"^Oil, olive, salad or cooking$", 0),
    ("rapsoel", "Rapsöl", "pflanzenöl|speiseöl", r"^Oil, canola$", 0),
    ("sonnenblumenoel", "Sonnenblumenöl", "", r"^Oil, sunflower, high oleic", 0),
    ("kokosoel", "Kokosöl", "kokosfett", r"^Oil, coconut$", 0),
    ("vollmilch", "Vollmilch (3,5 %)", "milch", r"^Milk, whole, 3.25% milkfat, with added vitamin D$", 0),
    ("fettarme-milch", "Fettarme Milch (1,5 %)", "magermilch|milch 1,5", r"^Milk, lowfat, fluid, 1% milkfat, with added vitamin A and vitamin D$", 0),
    ("sahne", "Schlagsahne (30 %)", "sahne|schlagrahm|rahm", r"^Cream, fluid, heavy whipping$", 0),
    ("kochsahne", "Kochsahne (leicht)", "", r"^Cream, fluid, light whipping$", 0),
    ("saure-sahne", "Saure Sahne / Schmand", "schmand|sauerrahm|crème fraîche", r"^Cream, sour, cultured$", 0),
    ("joghurt", "Naturjoghurt (3,5 %)", "joghurt", r"^Yogurt, plain, whole milk$", 0),
    ("frischkaese", "Frischkäse", "", r"^Cheese, cream$", 0),
    ("huettenkaese", "Hüttenkäse", "körniger frischkäse", r"^Cheese, cottage, creamed, large or small curd$", 0),
    ("ricotta", "Ricotta", "", r"^Cheese, ricotta, whole milk$", 0),
    ("mozzarella", "Mozzarella", "", r"^Cheese, mozzarella, whole milk$", 0),
    ("parmesan", "Parmesan / Hartkäse", "hartkäse|bergkäse", r"^Cheese, parmesan, hard$", 0),
    ("gouda", "Gouda", "käse|schnittkäse", r"^Cheese, gouda$", 0),
    ("emmentaler", "Emmentaler", "schweizer käse", r"^Cheese, swiss$", 0),
    ("cheddar", "Cheddar", "", r"^Cheese, cheddar \(", 0),
    ("feta", "Feta (Schafskäse)", "schafskäse|hirtenkäse", r"^Cheese, feta$", 0),
    ("haehnchenbrust", "Hähnchenbrust (ohne Haut)", "hähnchen|hühnerbrust|geflügel", r"^Chicken, broiler or fryers, breast, skinless, boneless, meat only, raw$", 0),
    ("putenbrust", "Putenbrust", "pute|truthahn", r"^Turkey, whole, breast, meat only, raw$", 0),
    ("rinderhack", "Rinderhackfleisch (20 % Fett)", "hackfleisch|hack|gehacktes", r"^Beef, ground, 80% lean meat / 20% fat, raw$", 0),
    ("rindersteak", "Rindersteak (Hüfte)", "rind|steak", r"^Beef, top sirloin, steak, separable lean and fat, trimmed to 1/8\" fat, all grades, raw$", 0),
    ("schweinehack", "Schweinehackfleisch", "", r"^Pork, fresh, ground, raw$", 0),
    ("schweinelende", "Schweinelende / Kotelett", "schwein|kotelett|schweinefleisch", r"^Pork, fresh, loin, whole, separable lean and fat, raw$", 0),
    ("kochschinken", "Kochschinken", "schinken", r"^Ham, sliced, regular", 0),
    ("lachs", "Lachs", "lachsfilet", r"^Fish, salmon, Atlantic, farmed, raw$", 0),
    ("kabeljau", "Kabeljau (Dorsch)", "dorsch|seelachs|fisch", r"^Fish, cod, Atlantic, raw$", 0),
    ("thunfisch-dose", "Thunfisch (Dose, im eigenen Saft)", "thunfisch", r"^Fish, tuna, light, canned in water, drained solids", 0),
    ("garnelen", "Garnelen", "shrimps|krabben|scampi", r"^Crustaceans, shrimp, raw$", 0),
    ("tofu", "Tofu (fest)", "", r"^Tofu, raw, firm, prepared with calcium sulfate$", 0),
    ("kartoffel", "Kartoffeln", "kartoffel|erdäpfel", r"^Potatoes, flesh and skin, raw$", 150),
    ("suesskartoffel", "Süßkartoffel", "", r"^Sweet potato, raw, unprepared", 200),
    ("zwiebel", "Zwiebel", "zwiebeln", r"^Onions, raw$", 0),
    ("lauchzwiebel", "Frühlingszwiebel", "lauchzwiebel", r"^Onions, spring or scallions", 0),
    ("schalotte", "Schalotte", "schalotten", r"^Shallots, raw$", 25),
    ("knoblauch", "Knoblauch", "knoblauchzehe|zehe", r"^Garlic, raw$", 0),
    ("lauch", "Lauch (Porree)", "porree", r"^Leeks, \(bulb and lower leaf-portion\), raw$", 0),
    ("tomate", "Tomate", "tomaten", r"^Tomatoes, red, ripe, raw, year round average$", 0),
    ("tomaten-dose", "Tomaten (Dose, stückig)", "dosentomaten|gehackte tomaten|passierte tomaten", r"^Tomatoes, red, ripe, canned, packed in tomato juice$", 0),
    ("tomatenmark", "Tomatenmark", "", r"^Tomato products, canned, paste, without salt added", 0),
    ("karotte", "Karotte (Möhre)", "möhre|möhren|karotten|mohrrübe", r"^Carrots, raw$", 0),
    ("paprika-rot", "Paprika (rot)", "paprikaschote|paprika", r"^Peppers, sweet, red, raw$", 0),
    ("chili", "Chilischote (rot)", "peperoni|chili", r"^Peppers, hot chili, red, raw$", 0),
    ("gurke", "Salatgurke", "gurke", r"^Cucumber, with peel, raw$", 0),
    ("zucchini", "Zucchini", "", r"^Squash, summer, zucchini, includes skin, raw$", 0),
    ("aubergine", "Aubergine", "", r"^Eggplant, raw$", 0),
    ("brokkoli", "Brokkoli", "broccoli", r"^Broccoli, raw$", 0),
    ("blumenkohl", "Blumenkohl", "karfiol", r"^Cauliflower, raw$", 0),
    ("weisskohl", "Weißkohl", "kohl|kraut", r"^Cabbage, raw$", 0),
    ("gruenkohl", "Grünkohl", "", r"^Kale, raw$", 0),
    ("spinat", "Spinat", "blattspinat", r"^Spinach, raw$", 0),
    ("champignons", "Champignons", "pilze|champignon", r"^Mushrooms, white, raw$", 0),
    ("eisbergsalat", "Eisbergsalat", "", r"^Lettuce, iceberg", 0),
    ("blattsalat", "Blattsalat", "salat|kopfsalat", r"^Lettuce, green leaf, raw$", 0),
    ("sellerie", "Staudensellerie", "sellerie", r"^Celery, raw$", 0),
    ("erbsen", "Erbsen (TK)", "erbsen", r"^Peas, green, frozen, unprepared", 0),
    ("mais", "Zuckermais", "mais", r"^Corn, sweet, yellow, raw$", 0),
    ("gruene-bohnen", "Grüne Bohnen", "bohnen|brechbohnen|prinzessbohnen", r"^Beans, snap, green, raw$", 0),
    ("spargel", "Spargel (grün)", "spargel", r"^Asparagus, raw$", 0),
    ("kuerbis", "Kürbis", "hokkaido", r"^Pumpkin, raw$", 0),
    ("rote-bete", "Rote Bete", "rote rübe", r"^Beets, raw$", 0),
    ("radieschen", "Radieschen", "", r"^Radishes, raw$", 0),
    ("ingwer", "Ingwer (frisch)", "", r"^Ginger root, raw$", 0),
    ("kidneybohnen", "Kidneybohnen (Dose, abgetropft)", "kidney", r"^Beans, kidney, red, mature seeds, canned, drained solids$", 0),
    ("kichererbsen", "Kichererbsen (Dose, abgetropft)", "kichererbse", r"^Chickpeas \(garbanzo beans, bengal gram\), mature seeds, canned, drained solids$", 0),
    ("linsen", "Linsen (getrocknet)", "linse|berglinsen", r"^Lentils, raw$", 0),
    ("reis", "Reis (weiß, roh)", "reis|langkornreis|basmati", r"^Rice, white, long-grain, regular, raw, unenriched$", 0),
    ("vollkornreis", "Naturreis (roh)", "vollkornreis|naturreis", r"^Rice, brown, long-grain, raw", 0),
    ("nudeln", "Nudeln (Hartweizen, roh)", "pasta|spaghetti|penne|nudeln", r"^Pasta, dry, unenriched$", 0),
    ("eiernudeln", "Eiernudeln (roh)", "bandnudeln|spätzle", r"^Noodles, egg, dry, unenriched$", 0),
    ("reisnudeln", "Reisnudeln (roh)", "glasnudeln", r"^Rice noodles, dry$", 0),
    ("couscous", "Couscous (roh)", "", r"^Couscous, dry$", 0),
    ("bulgur", "Bulgur (roh)", "", r"^Bulgur, dry$", 0),
    ("quinoa", "Quinoa (roh)", "", r"^Quinoa, uncooked$", 0),
    ("haferflocken", "Haferflocken", "hafer|porridge", r"^Cereals, oats, regular and quick, not fortified, dry$", 0),
    ("vollkornbrot", "Vollkornbrot (Weizen)", "vollkornbrot", r"^Bread, whole-wheat, commercially prepared$", 0),
    ("weissbrot", "Weißbrot / Toast", "toast|toastbrot|weißbrot", r"^Bread, white, commercially prepared \(includes soft bread crumbs\)$", 0),
    ("roggenbrot", "Roggenbrot", "graubrot|mischbrot", r"^Bread, rye$", 0),
    ("apfel", "Apfel", "äpfel", r"^Apples, raw, with skin", 180),
    ("banane", "Banane", "bananen", r"^Bananas, raw$", 0),
    ("birne", "Birne", "birnen", r"^Pears, raw, bartlett", 180),
    ("orange", "Orange", "apfelsine|orangen", r"^Oranges, raw, all commercial varieties$", 0),
    ("zitrone", "Zitrone", "zitronen", r"^Lemons, raw, without peel$", 0),
    ("zitronensaft", "Zitronensaft", "", r"^Lemon juice, raw$", 0),
    ("limette", "Limette", "", r"^Limes, raw$", 0),
    ("orangensaft", "Orangensaft (frisch)", "o-saft", r"^Orange juice, raw", 0),
    ("erdbeeren", "Erdbeeren", "erdbeere", r"^Strawberries, raw$", 0),
    ("heidelbeeren", "Heidelbeeren", "blaubeeren", r"^Blueberries, raw$", 0),
    ("himbeeren", "Himbeeren", "himbeere", r"^Raspberries, raw$", 0),
    ("trauben", "Weintrauben", "trauben", r"^Grapes, red or green", 0),
    ("kirschen", "Süßkirschen", "kirschen", r"^Cherries, sweet, raw$", 0),
    ("pfirsich", "Pfirsich", "nektarine", r"^Peaches, yellow, raw$", 0),
    ("pflaume", "Pflaumen / Zwetschgen", "zwetschgen|zwetschken", r"^Plums, raw$", 0),
    ("ananas", "Ananas", "", r"^Pineapple, raw, all varieties$", 0),
    ("kiwi", "Kiwi", "", r"^Kiwifruit, green, raw$", 0),
    ("mango", "Mango", "", r"^Mangos, raw$", 0),
    ("avocado", "Avocado", "", r"^Avocados, raw, all commercial varieties$", 0),
    ("rosinen", "Rosinen", "sultaninen", r"^Raisins, dark, seedless", 0),
    ("aprikosen-getrocknet", "Aprikosen (getrocknet)", "trockenaprikosen", r"^Apricots, dried, sulfured, uncooked$", 0),
    ("oliven", "Oliven (schwarz)", "", r"^Olives, ripe, canned \(small-extra large\)$", 0),
    ("mandeln", "Mandeln", "mandel", r"^Nuts, almonds$", 0),
    ("walnuesse", "Walnüsse", "walnuss", r"^Nuts, walnuts, english$", 0),
    ("haselnuesse", "Haselnüsse", "haselnuss", r"^Nuts, hazelnuts or filberts$", 0),
    ("erdnussbutter", "Erdnussbutter (ohne Salz)", "erdnussmus", r"^Peanut butter, smooth style, without salt$", 0),
    ("sonnenblumenkerne", "Sonnenblumenkerne", "", r"^Seeds, sunflower seed kernels, dried$", 0),
    ("kuerbiskerne", "Kürbiskerne", "", r"^Seeds, pumpkin and squash seed kernels, dried$", 0),
    ("sesam", "Sesam", "", r"^Seeds, sesame seeds, whole, dried$", 0),
    ("leinsamen", "Leinsamen", "", r"^Seeds, flaxseed$", 0),
    ("kokosmilch", "Kokosmilch (Dose)", "", r"^Nuts, coconut milk, canned", 0),
    ("sojadrink", "Sojadrink", "sojamilch", r"^Soymilk, original and vanilla, unfortified$", 0),
    ("mandeldrink", "Mandeldrink (ungesüßt)", "mandelmilch", r"^Beverages, almond milk, unsweetened, shelf stable$", 0),
    ("haehnchenbruehe", "Hühnerbrühe", "brühe|fond|bouillon|gemüsebrühe", r"^Soup, chicken broth, ready-to-serve$", 0),
    ("wasser", "Wasser", "leitungswasser", r"^Beverages, water, tap, drinking$", 0),
    ("kaffee", "Kaffee (gebrüht)", "", r"^Beverages, coffee, brewed, prepared with tap water$", 0),
    ("sojasauce", "Sojasauce", "sojasoße|shoyu", r"^Soy sauce made from soy \(tamari\)$", 0),
    ("senf", "Senf (mittelscharf)", "senf", r"^Mustard, prepared, yellow$", 0),
    ("ketchup", "Ketchup", "tomatenketchup", r"^Catsup$", 0),
    ("mayonnaise", "Mayonnaise", "mayo", r"^Salad dressing, mayonnaise, regular$", 0),
    ("apfelessig", "Essig (Apfelessig)", "essig|weinessig", r"^Vinegar, cider$", 0),
    ("balsamico", "Balsamico-Essig", "balsamico", r"^Vinegar, balsamic$", 0),
    ("pfeffer", "Pfeffer (schwarz)", "pfeffer", r"^Spices, pepper, black$", 0),
    ("paprikapulver", "Paprikapulver", "paprika edelsüß", r"^Spices, paprika$", 0),
    ("zimt", "Zimt (gemahlen)", "", r"^Spices, cinnamon, ground$", 0),
    ("kreuzkuemmel", "Kreuzkümmel", "cumin", r"^Spices, cumin seed$", 0),
    ("curry", "Currypulver", "curry", r"^Spices, curry powder$", 0),
    ("muskat", "Muskatnuss (gemahlen)", "muskat", r"^Spices, nutmeg, ground$", 0),
    ("oregano", "Oregano (getrocknet)", "", r"^Spices, oregano, dried$", 0),
    ("thymian", "Thymian (getrocknet)", "", r"^Spices, thyme, dried$", 0),
    ("knoblauchpulver", "Knoblauchpulver", "", r"^Spices, garlic powder$", 0),
    ("ingwerpulver", "Ingwer (gemahlen)", "", r"^Spices, ginger, ground$", 0),
    ("basilikum", "Basilikum (frisch)", "", r"^Basil, fresh$", 0),
    ("petersilie", "Petersilie (frisch)", "", r"^Parsley, fresh$", 0),
    ("schnittlauch", "Schnittlauch", "", r"^Chives, raw$", 0),
    ("dill", "Dill (frisch)", "", r"^Dill weed, fresh$", 0),
    ("vanillearoma", "Vanillearoma (flüssig)", "vanille|vanilleextrakt", r"^Vanilla extract, imitation, no alcohol$", 0),
]

# Abteilung im Laden (für die Einkaufsliste) aus der USDA-Lebensmittelgruppe; Ausnahmen je Kennung
CAT = {"1": "Milch, Käse & Eier", "2": "Gewürze & Soßen", "4": "Öle & Fette", "5": "Fleisch & Fisch", "6": "Vorrat", "7": "Fleisch & Fisch", "8": "Getreide, Nudeln & Reis",
       "9": "Obst & Gemüse", "10": "Fleisch & Fisch", "11": "Obst & Gemüse", "12": "Nüsse & Samen", "13": "Fleisch & Fisch", "14": "Getränke", "15": "Fleisch & Fisch",
       "16": "Vorrat", "18": "Brot & Backzutaten", "19": "Süßes & Backzutaten", "20": "Getreide, Nudeln & Reis"}
OVERRIDE = {"butter": "Milch, Käse & Eier", "margarine": "Milch, Käse & Eier", "tofu": "Kühlregal", "kokosmilch": "Vorrat", "sojadrink": "Getränke", "mandeldrink": "Getränke",
            "tomaten-dose": "Vorrat", "tomatenmark": "Vorrat", "kidneybohnen": "Vorrat", "kichererbsen": "Vorrat", "thunfisch-dose": "Vorrat", "oliven": "Vorrat",
            "rosinen": "Nüsse & Samen", "aprikosen-getrocknet": "Nüsse & Samen", "erdnussbutter": "Vorrat", "speisestaerke": "Brot & Backzutaten",
            "weizenmehl": "Brot & Backzutaten", "vollkornmehl": "Brot & Backzutaten", "brotmehl": "Brot & Backzutaten", "roggenmehl": "Brot & Backzutaten", "griess": "Brot & Backzutaten",
            "basilikum": "Obst & Gemüse", "petersilie": "Obst & Gemüse", "schnittlauch": "Obst & Gemüse", "dill": "Obst & Gemüse", "ingwer": "Obst & Gemüse", "salz": "Gewürze & Soßen", "wasser": "Getränke"}

N = {"kcal": "1008", "protein": "1003", "fett": "1004", "gesfett": "1258", "kh": "1005", "zucker": "2000", "ballast": "1079", "natrium": "1093"}

foods = list(csv.DictReader(open(f"{src}/food.csv", encoding="utf-8")))
want = {}
for key, name, syn, pat, piece in MAP:
    hit = [f for f in foods if re.search(pat, f["description"])]
    if not hit:
        sys.exit(f"nicht gefunden: {key} {pat}")
    want[hit[0]["fdc_id"]] = (key, name, syn, hit[0]["description"], piece, OVERRIDE.get(key) or CAT.get(hit[0]["food_category_id"], "Sonstiges"))

nut = {fid: {} for fid in want}
with open(f"{src}/food_nutrient.csv", encoding="utf-8") as fh:
    for r in csv.DictReader(fh):
        if r["fdc_id"] in nut and r["nutrient_id"] in N.values():
            nut[r["fdc_id"]][r["nutrient_id"]] = float(r["amount"])

# Dichte (g/ml) aus Portionsangaben „cup“ (236,588 ml) bzw. „tbsp“ (14,787 ml) / „tsp“ (4,929 ml); Stückgewicht aus „large/medium/clove/whole“
VOL = {"cup": 236.588, "tbsp": 14.787, "tablespoon": 14.787, "tsp": 4.929, "teaspoon": 4.929, "fl oz": 29.574}
units = {r["id"]: r["name"] for r in csv.DictReader(open(f"{src}/measure_unit.csv", encoding="utf-8"))}
dens, piece = {}, {}
with open(f"{src}/food_portion.csv", encoding="utf-8") as fh:
    for r in csv.DictReader(fh):
        fid = r["fdc_id"]
        if fid not in want:
            continue
        mod = (r["modifier"] or "").lower(); amt = float(r["amount"] or 1); g = float(r["gram_weight"] or 0)
        if g <= 0 or amt <= 0:
            continue
        u = units.get(r["measure_unit_id"], "")
        unit = u if u in VOL else next((k for k in VOL if mod == k or mod.startswith(k + " ") or mod.startswith(k + ",")), None)
        if unit and fid not in dens and "chopped" not in mod and "sliced" not in mod and "pieces" not in mod and "halves" not in mod and "cubes" not in mod and "diced" not in mod and "pressed" not in mod and "pureed" not in mod and "whipped" not in mod and "grated" not in mod and "shredded" not in mod and "packed" not in mod and "crumbled" not in mod and "mashed" not in mod and "sifted" not in mod:
            dens[fid] = round(g / (amt * VOL[unit]), 3)
        m = re.match(r"^(medium|large|clove|fruit|whole|pepper|extra large|small)\b", mod)
        if m and "slice" not in mod:
            rank = ["medium", "large", "clove", "fruit", "whole", "pepper", "extra large", "small"].index(m.group(1))
            if fid not in piece or rank < piece[fid][1]:
                piece[fid] = (round(g / amt, 1), rank)

piece = {k: v[0] for k, v in piece.items()}

def num(v, dec=1):
    return f"{v:.{dec}f}".rstrip("0").rstrip(".") if v is not None else "-1"

lines, tsv = [], ["kennung\tname\tfdc_id\tbeschreibung_usda\tdichte_g_ml\tstueck_g\tabteilung"]
for fid, (key, name, syn, desc, pc, gang) in want.items():
    n = nut[fid]
    kcal = n.get(N["kcal"])
    if kcal is None:
        sys.exit(f"keine Energie: {key}")
    salt = n.get(N["natrium"]); salt = None if salt is None else salt * 2.5 / 1000
    d = dens.get(fid, 0); p = pc or piece.get(fid, 0)
    vals = [kcal, n.get(N["protein"]), n.get(N["fett"]), n.get(N["gesfett"]), n.get(N["kh"]), n.get(N["zucker"]), n.get(N["ballast"]), salt]
    lines.append(f'        new("{key}", "{name}", "{syn}", {fid}, {", ".join(num(v, 2 if i == 7 else 1) for i, v in enumerate(vals))}, {num(d, 2) if d else "0"}, {num(p) if p else "0"}, "{gang}"),')
    tsv.append(f"{key}\t{name}\t{fid}\t{desc}\t{d}\t{p}\t{gang}")

cs = f"""// <auto-generated> T-0787: erzeugt mit tools/rezepte/naehrwerte_gen.py aus USDA FoodData Central, SR Legacy (April 2018), CC0 1.0 – nicht von Hand ändern.
namespace Trendo.Core.Wissen;

public static partial class NaehrwertTabelle
{{
    /// <summary>{len(lines)} Lebensmittel, Werte je 100 g (essbarer Anteil). −1 = in der Quelle nicht angegeben.
    /// Spalten: Kennung, Name, Suchwörter, FDC-Nummer, kcal, Eiweiß, Fett, davon gesättigt, Kohlenhydrate, davon Zucker, Ballaststoffe, Salz (aus Natrium × 2,5), Dichte g/ml (0 = unbekannt), Gramm je Stück (0 = unbekannt), Abteilung im Laden.</summary>
    public static readonly Lebensmittel[] Alle =
    [
{chr(10).join(lines)}
    ];
}}
"""
open(out_cs, "w", encoding="utf-8", newline="\n").write(cs)
open(out_tsv, "w", encoding="utf-8", newline="\n").write("\n".join(tsv) + "\n")
print(len(lines), "Lebensmittel")
