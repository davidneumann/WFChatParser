import json
import codecs

def bom_open(path, *args, **kwargs):
    with open(path, 'rb') as f:
        raw = f.read(4)
    for enc, boms in \
            ('utf-8-sig', (codecs.BOM_UTF8,)), \
            ('utf-32', (codecs.BOM_UTF32_LE, codecs.BOM_UTF32_BE)), \
            ('utf-16', (codecs.BOM_UTF16_LE, codecs.BOM_UTF16_BE)):
        if any(raw.startswith(bom) for bom in boms):
            return open(path, *args, **kwargs, encoding=enc)
    return open(path, *args, **kwargs)

ligatures = []
with bom_open("gapcombos.txt", "r") as f:
	for line in f.read().split("\n"):
		if "," in line:
			(left, right) = line.split(",")
			ligatures.append({
				"left": left,
				"right": right,
				"name": line,
				})

with open("gaps.json", "rb") as f:
	gaps = json.loads(f.read())
	lefts = []
	rights = []
	for ligature in ligatures:
		# print(ligature)
		for gap in gaps:
			if gap.get("Right") == ligature["left"]:
				rights.append({
					"Left": gap.get("Left"),
					"Right": ligature["name"],
					"Gap": gap.get("Gap")
					})
			elif gap.get("Left") == ligature["right"]:
				lefts.append({
					"Left": ligature["name"],
					"Right": gap.get("Right"),
					"Gap": gap.get("Gap")
					})

with open("newgaps.json", "wb") as f:
	f.write(json.dumps(gaps + lefts + rights).encode())