import json

ligatures = []
with open("gapcombos.txt", "r") as f:
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