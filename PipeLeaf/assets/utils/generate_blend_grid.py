ingredients = [
    {
      "code": "pipeleaf"
    },
    {
      "code": "catmint"
    },
    {
      "code": "orangemallow"
    },
    {
      "code": "poisonoak"
    },
    {
      "code": "goldenpoppy"
    },
    {
      "code": "cornflower"
    },
    {
      "code": "chamomile"
    },
    {
      "code": "lavender"
    },
    {
      "code": "marjoram"
    },
    {
      "code": "marshmallow"
    },
    {
      "code": "mint"
    },
    {
      "code": "rosemary"
    },
    {
      "code": "saffron"
    },
    {
      "code": "sage"
    },
    {
      "code": "stingingnettle"
    },
    {
      "code": "stjohnswort"
    },
    {
      "code": "thyme"
    },
    {
      "code": "mugwort"
    },
    {
      "code": "chicory"
    },
    {
      "code": "marigold"
    },
    {
      "code": "yarrow"
    },
    {
      "code": "angelica"
    },
    {
      "code": "arnica"
    },
    {
      "code": "horehound"
    },
    {
      "code": "ginseng"
    }
]
ingredients = [i['code'] for i in ingredients]
  
combos = []
for ing in ingredients:
    if ing == 'pipeleaf': continue
    for inner in ingredients:
        if inner == ing: continue
        if inner == 'pipeleaf': continue
        combos.append((ing, inner))
        
# print(combos)

patches = []

for ing1, ing2 in combos:
    patches.append(  {
    "ingredientPattern": "_P_H_S",
    "ingredients": {
      "P": {
        "type": "item",
        "code": "pipeleaf:smokable-pipeleaf-shag",
        "quantity": 3
      },
      "H": {
        "type": "item",
        "code": f"pipeleaf:smokable-{ing1}-shag",
        "quantity": 2
      },
      "S": {
        "type": "item",
        "code": f"pipeleaf:smokable-{ing2}-shag",
        "quantity": 1
        }
    },
    "width": 3,
    "height": 2,
    "output": {
      "type": "item",
      "code": f"pipeleaf:shagblend-{ing1}-{ing2}",
      "quantity": 6
    }
  })
  
#print(patches)

import json
print(json.dumps(patches))
