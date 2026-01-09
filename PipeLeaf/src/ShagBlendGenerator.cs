using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PipeLeaf
{
    // Data models
    public class SmokableEffect
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("amount")]
        public double Amount { get; set; }

        [JsonPropertyName("cooldown")]
        public int Cooldown { get; set; }
    }

    public class SmokableAttributes
    {
        [JsonPropertyName("smokableEffects")]
        public List<SmokableEffect> SmokableEffects { get; set; }
    }

    public class SmokableData
    {
        [JsonPropertyName("attributesByType")]
        public Dictionary<string, SmokableAttributes> AttributesByType { get; set; }
    }

    public class SmokablesVariant
    {
        [JsonPropertyName("code")]
        public string Code { get; set; }
    }

    public class SmokablesData
    {
        [JsonPropertyName("variants")]
        public List<SmokablesVariant> Variants { get; set; }
    }

    public class IngredientEffect
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("amount")]
        public double Amount { get; set; }

        [JsonPropertyName("cooldown")]
        public int Cooldown { get; set; }
    }

    public class IngredientDefinition
    {
        [JsonPropertyName("code")]
        public string Code { get; set; }

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; }

        [JsonPropertyName("effects")]
        public List<IngredientEffect> Effects { get; set; }

        [JsonPropertyName("handbookDescription")]
        public string HandbookDescription { get; set; }
    }

    public class IngredientsConfig
    {
        [JsonPropertyName("ingredients")]
        public List<IngredientDefinition> Ingredients { get; set; }

        [JsonPropertyName("defaultEffect")]
        public IngredientEffect DefaultEffect { get; set; }
    }

    public class RecipeIngredient
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "item";

        [JsonPropertyName("code")]
        public string Code { get; set; }

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }
    }

    public class Recipe
    {
        [JsonPropertyName("ingredientPattern")]
        public string IngredientPattern { get; set; }

        [JsonPropertyName("ingredients")]
        public Dictionary<string, RecipeIngredient> Ingredients { get; set; }

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }

        [JsonPropertyName("output")]
        public RecipeIngredient Output { get; set; }
    }

    public class BlendRule
    {
        public string EffectType { get; set; }
        public Func<double, double, double> CombineFunction { get; set; }
        public int Priority { get; set; }
    }

    public class SpecialBlend
    {
        public string Ingredient1 { get; set; }
        public string Ingredient2 { get; set; }
        public string FlavorName { get; set; }
        public string Description { get; set; }
        public Dictionary<string, object> AdditionalProperties { get; set; } = new Dictionary<string, object>();
    }

    public class BlendGeneratorConfig
    {
        public string IngredientsConfigPath { get; set; }
        public string SmokableJsonPath { get; set; }
        public string SmokablesJsonPath { get; set; }
        public string OutputShagblendPath { get; set; }
        public string OutputRecipesPath { get; set; }
        public string OutputCuredBlendPath { get; set; }
        public string OutputCuredRecipesPath { get; set; }
        public string OutputLangPath { get; set; }
        public string OutputBlendable1Path { get; set; }
        public string OutputBlendable2Path { get; set; }
        public List<string> ExcludeIngredients { get; set; } = new List<string> { "pipeleaf" };
        public int MaxEffectsPerBlend { get; set; } = 2;
        public string FillerIngredient { get; set; } = "pipeleaf";
        public int RecipeWidth { get; set; } = 3;
        public int RecipeHeight { get; set; } = 2;
        public int TotalRecipeQuantity { get; set; } = 6;
        public int PrimaryIngredientQuantity { get; set; } = 3;
        public int SecondaryIngredientQuantity { get; set; } = 2;
        public int FillerQuantity { get; set; } = 1;
        public List<SpecialBlend> SpecialBlends { get; set; } = new List<SpecialBlend>();
        public bool GenerateFromConfig { get; set; } = false;
    }

    public class ShagBlendGenerator
    {
        private readonly BlendGeneratorConfig _config;
        private readonly List<BlendRule> _blendRules;
        private Dictionary<string, List<SmokableEffect>> _ingredientEffects;
        private List<string> _allIngredients;
        private Dictionary<string, SpecialBlend> _specialBlendLookup;
        private Dictionary<string, string> _blendDisplayNames;
        private Dictionary<string, string> _ingredientDisplayNames;
        private IngredientsConfig _ingredientsConfig;

        public ShagBlendGenerator(BlendGeneratorConfig config)
        {
            _config = config;
            _blendRules = InitializeBlendRules();
            _blendDisplayNames = new Dictionary<string, string>();
            _ingredientDisplayNames = new Dictionary<string, string>();

            // Build special blend lookup for fast access
            _specialBlendLookup = new Dictionary<string, SpecialBlend>();
            foreach (var special in _config.SpecialBlends)
            {
                var key1 = $"{special.Ingredient1}-{special.Ingredient2}";
                var key2 = $"{special.Ingredient2}-{special.Ingredient1}";
                _specialBlendLookup[key1] = special;
                _specialBlendLookup[key2] = special;
            }
        }

        private List<BlendRule> InitializeBlendRules()
        {
            return new List<BlendRule>
            {
                new BlendRule
                {
                    EffectType = "temporalstability",
                    CombineFunction = (a, b) => a + b,
                    Priority = 100
                },
                new BlendRule
                {
                    EffectType = "intoxication",
                    CombineFunction = (a, b) => Math.Max(a, b),
                    Priority = 90
                },
                new BlendRule
                {
                    EffectType = "healthpoints",
                    CombineFunction = (a, b) => Math.Min(a, b),
                    Priority = 95
                },
                new BlendRule
                {
                    EffectType = "tiredness",
                    CombineFunction = (a, b) => Math.Max(a, b),
                    Priority = 85
                },
                new BlendRule
                {
                    EffectType = "bodytemperature",
                    CombineFunction = (a, b) => Math.Max(a, b),
                    Priority = 70
                },
                new BlendRule
                {
                    EffectType = "hungerrate",
                    CombineFunction = (a, b) => Math.Min(a, b),
                    Priority = 80
                }
            };
        }

        public void Generate()
        {
            if (_config.GenerateFromConfig && !string.IsNullOrEmpty(_config.IngredientsConfigPath))
            {
                Console.WriteLine("Loading ingredients from config...");
                LoadIngredientsFromConfig();

                Console.WriteLine("Generating smokable.json...");
                WriteSmokableJson();

                Console.WriteLine("Generating world properties...");
                WriteWorldProperties();
            }
            else
            {
                Console.WriteLine("Loading smokable data...");
                LoadIngredientData();
            }

            Console.WriteLine($"Found {_allIngredients.Count} smokable ingredients");

            Console.WriteLine("Generating blend combinations...");
            var blends = GenerateBlends();

            Console.WriteLine($"Generated {blends.Count} blend combinations");

            Console.WriteLine("Generating recipes...");
            var recipes = GenerateRecipes(blends);

            Console.WriteLine($"Generated {recipes.Count} recipes");

            Console.WriteLine("Writing shagblend.json...");
            WriteShagblendJson(blends);

            Console.WriteLine("Writing recipes json...");
            WriteRecipesJson(recipes);

            Console.WriteLine("Generating cured blend recipes...");
            var curedRecipes = GenerateCuredRecipes(blends);

            Console.WriteLine($"Generated {curedRecipes.Count} cured recipes");

            Console.WriteLine("Writing cured-blends.json...");
            WriteCuredBlendJson(blends);

            Console.WriteLine("Writing cured recipes json...");
            WriteCuredRecipesJson(curedRecipes);

            Console.WriteLine("Writing language file...");
            WriteLangFile();

            Console.WriteLine("Generation complete!");
        }

        private void LoadIngredientsFromConfig()
        {
            var jsonOptions = new JsonSerializerOptions
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };

            var configJson = File.ReadAllText(_config.IngredientsConfigPath);
            _ingredientsConfig = JsonSerializer.Deserialize<IngredientsConfig>(configJson, jsonOptions);

            _allIngredients = _ingredientsConfig.Ingredients
                .Select(i => i.Code)
                .Where(code => !_config.ExcludeIngredients.Contains(code))
                .ToList();

            _ingredientEffects = new Dictionary<string, List<SmokableEffect>>();
            _ingredientDisplayNames = new Dictionary<string, string>();

            foreach (var ingredient in _ingredientsConfig.Ingredients)
            {
                // Always store display names for ALL ingredients (for language file generation)
                _ingredientDisplayNames[ingredient.Code] = ingredient.DisplayName;

                // Only store effects for non-excluded ingredients (for blending)
                if (!_config.ExcludeIngredients.Contains(ingredient.Code))
                {
                    // Convert IngredientEffect to SmokableEffect
                    _ingredientEffects[ingredient.Code] = ingredient.Effects.Select(e => new SmokableEffect
                    {
                        Type = e.Type,
                        Amount = e.Amount,
                        Cooldown = e.Cooldown
                    }).ToList();
                }
            }
        }

        private void LoadIngredientData()
        {
            var jsonOptions = new JsonSerializerOptions
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };

            var smokablesJson = File.ReadAllText(_config.SmokablesJsonPath);
            var smokablesData = JsonSerializer.Deserialize<SmokablesData>(smokablesJson, jsonOptions);
            _allIngredients = smokablesData.Variants
                .Select(v => v.Code)
                .Where(code => !_config.ExcludeIngredients.Contains(code))
                .ToList();

            var smokableJson = File.ReadAllText(_config.SmokableJsonPath);
            var smokableData = JsonSerializer.Deserialize<SmokableData>(smokableJson, jsonOptions);

            _ingredientEffects = new Dictionary<string, List<SmokableEffect>>();

            foreach (var ingredient in _allIngredients)
            {
                var key = $"smokable-{ingredient}-shag";
                if (smokableData.AttributesByType.ContainsKey(key))
                {
                    _ingredientEffects[ingredient] = smokableData.AttributesByType[key].SmokableEffects;
                }
                else
                {
                    _ingredientEffects[ingredient] = new List<SmokableEffect>
                    {
                        new SmokableEffect
                        {
                            Type = "bodytemperature",
                            Amount = 5,
                            Cooldown = 0
                        }
                    };
                }
            }
        }

        private Dictionary<string, List<SmokableEffect>> GenerateBlends()
        {
            var blends = new Dictionary<string, List<SmokableEffect>>();

            for (int i = 0; i < _allIngredients.Count; i++)
            {
                for (int j = i + 1; j < _allIngredients.Count; j++)
                {
                    var ingredient1 = _allIngredients[i];
                    var ingredient2 = _allIngredients[j];

                    var blendName1 = $"shagblend-{ingredient1}-{ingredient2}";
                    var blendName2 = $"shagblend-{ingredient2}-{ingredient1}";

                    var combinedEffects = CombineEffects(
                        _ingredientEffects[ingredient1],
                        _ingredientEffects[ingredient2]
                    );

                    blends[blendName1] = combinedEffects;
                    blends[blendName2] = combinedEffects;

                    // Generate display names for language file
                    var lookupKey = $"{ingredient1}-{ingredient2}";
                    if (_specialBlendLookup.ContainsKey(lookupKey))
                    {
                        // Use special flavor name
                        var special = _specialBlendLookup[lookupKey];
                        _blendDisplayNames[blendName1] = special.FlavorName;
                        _blendDisplayNames[blendName2] = special.FlavorName;
                    }
                    else
                    {
                        // Generate default names
                        _blendDisplayNames[blendName1] = GenerateDefaultBlendName(ingredient1, ingredient2);
                        _blendDisplayNames[blendName2] = GenerateDefaultBlendName(ingredient2, ingredient1);
                    }
                }
            }

            return blends;
        }

        private List<SmokableEffect> CombineEffects(
            List<SmokableEffect> effects1,
            List<SmokableEffect> effects2)
        {
            var combinedEffects = new Dictionary<string, SmokableEffect>();

            foreach (var effect in effects1.Concat(effects2))
            {
                if (combinedEffects.ContainsKey(effect.Type))
                {
                    var rule = _blendRules.FirstOrDefault(r => r.EffectType == effect.Type);
                    if (rule != null)
                    {
                        var existing = combinedEffects[effect.Type];
                        existing.Amount = rule.CombineFunction(existing.Amount, effect.Amount);
                    }
                }
                else
                {
                    combinedEffects[effect.Type] = new SmokableEffect
                    {
                        Type = effect.Type,
                        Amount = effect.Amount,
                        Cooldown = effect.Cooldown
                    };
                }
            }

            var sortedEffects = combinedEffects.Values
                .OrderByDescending(e =>
                {
                    var rule = _blendRules.FirstOrDefault(r => r.EffectType == e.Type);
                    return rule?.Priority ?? 0;
                })
                .Take(_config.MaxEffectsPerBlend)
                .ToList();

            return sortedEffects;
        }

        private List<Recipe> GenerateRecipes(Dictionary<string, List<SmokableEffect>> blends)
        {
            var recipes = new List<Recipe>();
            var letterMapping = new Dictionary<string, string>();
            var usedLetters = new HashSet<string>();

            foreach (var blendName in blends.Keys)
            {
                var parts = blendName.Replace("shagblend-", "").Split('-');
                if (parts.Length != 2) continue;

                var ingredient1 = parts[0];
                var ingredient2 = parts[1];

                if (!letterMapping.ContainsKey(ingredient1))
                    letterMapping[ingredient1] = GetUniqueLetterCode(ingredient1, usedLetters);

                if (!letterMapping.ContainsKey(ingredient2))
                    letterMapping[ingredient2] = GetUniqueLetterCode(ingredient2, usedLetters);

                var letter1 = "A";
                var letter2 = "B";
                var fillerLetter = "S";

                var pattern = $"_{letter1}_{letter2}_{fillerLetter}";

                var recipe = new Recipe
                {
                    IngredientPattern = pattern,
                    Ingredients = new Dictionary<string, RecipeIngredient>
                    {
                        [letter1] = new RecipeIngredient
                        {
                            Code = $"pipeleaf:smokable-{ingredient1}-shag",
                            Quantity = _config.PrimaryIngredientQuantity
                        },
                        [letter2] = new RecipeIngredient
                        {
                            Code = $"pipeleaf:smokable-{ingredient2}-shag",
                            Quantity = _config.SecondaryIngredientQuantity
                        },
                        [fillerLetter] = new RecipeIngredient
                        {
                            Code = $"pipeleaf:smokable-{_config.FillerIngredient}-shag",
                            Quantity = _config.FillerQuantity
                        }
                    },
                    Width = _config.RecipeWidth,
                    Height = _config.RecipeHeight,
                    Output = new RecipeIngredient
                    {
                        Code = $"pipeleaf:{blendName}",
                        Quantity = _config.TotalRecipeQuantity
                    }
                };

                recipes.Add(recipe);
            }

            return recipes;
        }

        private string GetUniqueLetterCode(string ingredient, HashSet<string> usedLetters)
        {
            var letter = ingredient[0].ToString().ToUpper();
            if (!usedLetters.Contains(letter))
            {
                usedLetters.Add(letter);
                return letter;
            }

            for (int i = 1; i < ingredient.Length; i++)
            {
                letter = ingredient[i].ToString().ToUpper();
                if (!usedLetters.Contains(letter) && char.IsLetter(letter[0]))
                {
                    usedLetters.Add(letter);
                    return letter;
                }
            }

            var baseLetter = ingredient[0].ToString().ToUpper();
            int counter = 1;
            while (usedLetters.Contains($"{baseLetter}{counter}"))
                counter++;

            var result = $"{baseLetter}{counter}";
            usedLetters.Add(result);
            return result;
        }

        private void WriteShagblendJson(Dictionary<string, List<SmokableEffect>> blends)
        {
            var skipVariants = _allIngredients
                .Select(i => $"shagblend-{i}-{i}")
                .Concat(new[] { "*-pipeleaf-*", "*-*-pipeleaf" })
                .ToList();

            // Build attributesByType with merged special properties
            var attributesByType = new Dictionary<string, object>();
            foreach (var kvp in blends)
            {
                var blendKey = kvp.Key;
                var baseAttributes = new Dictionary<string, object>
                {
                    ["smokableEffects"] = kvp.Value
                };

                // Check if this is a special blend
                var ingredientKey = blendKey.Replace("shagblend-", "");
                if (_specialBlendLookup.ContainsKey(ingredientKey))
                {
                    var special = _specialBlendLookup[ingredientKey];
                    // Merge additional properties
                    foreach (var prop in special.AdditionalProperties)
                    {
                        baseAttributes[prop.Key] = prop.Value;
                    }
                }

                attributesByType[blendKey] = baseAttributes;
            }

            var output = new
            {
                code = "shagblend",
                enabled = true,
                @class = "SmokableItem",
                creativeinventory = new
                {
                    general = new[] { "*" },
                    items = new[] { "*" }
                },
                materialDensity = 300,
                maxstacksize = 64,
                variantgroups = new[]
                {
                    new { loadFromProperties = "pipeleaf:item/blendable1" },
                    new { loadFromProperties = "pipeleaf:item/blendable2" }
                },
                skipVariants,
                textureByType = new Dictionary<string, object>
                {
                    ["shagblend-*"] = new { @base = "pipeleaf:item/smokable/shag" }
                },
                attributesByType,
                transitionableProps = new[]
                {
                    new
                    {
                        type = "Perish",
                        freshHoursByType = new Dictionary<string, object>
                        {
                            ["*"] = new { avg = 8640 }
                        },
                        transitionHours = new { avg = 12 },
                        transitionedStack = new
                        {
                            type = "item",
                            code = "game:rot"
                        },
                        transitionRatioByType = new Dictionary<string, int>
                        {
                            ["*"] = 1
                        }
                    }
                },
                nutritionProps = new
                {
                    satiety = 15,
                    foodcategory = "Vegetable"
                }
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.Never
            };

            var json = JsonSerializer.Serialize(output, options);
            File.WriteAllText(_config.OutputShagblendPath, json);
        }

        private void WriteRecipesJson(List<Recipe> recipes)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.Never
            };

            var json = JsonSerializer.Serialize(recipes, options);
            File.WriteAllText(_config.OutputRecipesPath, json);
        }

        private string GenerateDefaultBlendName(string primary, string secondary)
        {
            // Capitalize first letter of each ingredient
            var primaryName = char.ToUpper(primary[0]) + primary.Substring(1);
            var secondaryName = char.ToUpper(secondary[0]) + secondary.Substring(1);

            return $"{primaryName}-{secondaryName} Blend";
        }

        private void WriteLangFile()
        {
            // Load existing language file if it exists
            Dictionary<string, string> langEntries;

            if (File.Exists(_config.OutputLangPath))
            {
                var existingJson = File.ReadAllText(_config.OutputLangPath);
                var jsonOptions = new JsonSerializerOptions
                {
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                };
                langEntries = JsonSerializer.Deserialize<Dictionary<string, string>>(existingJson, jsonOptions)
                              ?? new Dictionary<string, string>();
            }
            else
            {
                langEntries = new Dictionary<string, string>();
            }

            // Remove old blend entries (in case we're regenerating)
            var keysToRemove = langEntries.Keys
                .Where(k => k.StartsWith("pipeleaf:item-shagblend-") || k.StartsWith("pipeleaf:itemdesc-shagblend-") ||
                            k.StartsWith("pipeleaf:item-curedblend-") || k.StartsWith("pipeleaf:itemdesc-curedblend-") ||
                            k.StartsWith("pipeleaf:item-smokable-"))
                .ToList();
            foreach (var key in keysToRemove)
            {
                langEntries.Remove(key);
            }

            // Add ingredient display names if generated from config
            if (_config.GenerateFromConfig && _ingredientDisplayNames.Any())
            {
                foreach (var kvp in _ingredientDisplayNames)
                {
                    var curedKey = $"pipeleaf:item-smokable-{kvp.Key}-cured";
                    var shagKey = $"pipeleaf:item-smokable-{kvp.Key}-shag";
                    langEntries[curedKey] = $"{kvp.Value} (Cured)";
                    langEntries[shagKey] = $"{kvp.Value} (Shag)";
                }
            }

            // Add entries for each blend (both shag and cured use same display names)
            foreach (var kvp in _blendDisplayNames)
            {
                var blendCode = kvp.Key.Replace("shagblend-", "");

                // Shagblend entry
                var shagKey = $"pipeleaf:item-shagblend-{blendCode}";
                langEntries[shagKey] = kvp.Value;

                // Curedblend entry (same name)
                var curedKey = $"pipeleaf:item-curedblend-{blendCode}";
                langEntries[curedKey] = kvp.Value;
            }

            // Add descriptions for special blends
            foreach (var special in _config.SpecialBlends)
            {
                if (!string.IsNullOrEmpty(special.Description))
                {
                    var shagKey1 = $"pipeleaf:itemdesc-shagblend-{special.Ingredient1}-{special.Ingredient2}";
                    var shagKey2 = $"pipeleaf:itemdesc-shagblend-{special.Ingredient2}-{special.Ingredient1}";
                    var curedKey1 = $"pipeleaf:itemdesc-curedblend-{special.Ingredient1}-{special.Ingredient2}";
                    var curedKey2 = $"pipeleaf:itemdesc-curedblend-{special.Ingredient2}-{special.Ingredient1}";

                    langEntries[shagKey1] = special.Description;
                    langEntries[shagKey2] = special.Description;
                    langEntries[curedKey1] = special.Description;
                    langEntries[curedKey2] = special.Description;
                }
            }

            // Sort keys for readability
            var sortedEntries = langEntries.OrderBy(kvp => kvp.Key)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            var json = JsonSerializer.Serialize(sortedEntries, options);
            File.WriteAllText(_config.OutputLangPath, json);

            Console.WriteLine($"Added/updated {_blendDisplayNames.Count * 2} blend entries in language file");
        }

        private List<Recipe> GenerateCuredRecipes(Dictionary<string, List<SmokableEffect>> blends)
        {
            var recipes = new List<Recipe>();

            foreach (var blendName in blends.Keys)
            {
                var parts = blendName.Replace("shagblend-", "").Split('-');
                if (parts.Length != 2) continue;

                var ingredient1 = parts[0];
                var ingredient2 = parts[1];

                var letter1 = "A";
                var letter2 = "B";
                var fillerLetter = "S";

                var pattern = $"_{letter1}_{letter2}_{fillerLetter}";

                var recipe = new Recipe
                {
                    IngredientPattern = pattern,
                    Ingredients = new Dictionary<string, RecipeIngredient>
                    {
                        [letter1] = new RecipeIngredient
                        {
                            Code = $"pipeleaf:smokable-{ingredient1}-cured",
                            Quantity = _config.PrimaryIngredientQuantity
                        },
                        [letter2] = new RecipeIngredient
                        {
                            Code = $"pipeleaf:smokable-{ingredient2}-cured",
                            Quantity = _config.SecondaryIngredientQuantity
                        },
                        [fillerLetter] = new RecipeIngredient
                        {
                            Code = $"pipeleaf:smokable-{_config.FillerIngredient}-cured",
                            Quantity = _config.FillerQuantity
                        }
                    },
                    Width = _config.RecipeWidth,
                    Height = _config.RecipeHeight,
                    Output = new RecipeIngredient
                    {
                        Code = $"pipeleaf:curedblend-{ingredient1}-{ingredient2}",
                        Quantity = _config.TotalRecipeQuantity
                    }
                };

                recipes.Add(recipe);
            }

            return recipes;
        }

        private void WriteCuredBlendJson(Dictionary<string, List<SmokableEffect>> blends)
        {
            var skipVariants = _allIngredients
                .Select(i => $"curedblend-{i}-{i}")
                .Concat(new[] { "*-pipeleaf-*", "*-*-pipeleaf" })
                .ToList();

            var output = new
            {
                code = "curedblend",
                enabled = true,
                creativeinventory = new
                {
                    general = new[] { "*" },
                    items = new[] { "*" }
                },
                materialDensity = 300,
                maxstacksize = 64,
                variantgroups = new[]
                {
                    new { loadFromProperties = "pipeleaf:item/blendable1" },
                    new { loadFromProperties = "pipeleaf:item/blendable2" }
                },
                skipVariants,
                textureByType = new Dictionary<string, object>
                {
                    ["curedblend-*"] = new { @base = "pipeleaf:item/smokable/cured" }
                },
                grindingPropsByType = new Dictionary<string, object>
                {
                    ["curedblend-*"] = new
                    {
                        GroundStack = new
                        {
                            type = "item",
                            stacksize = 1,
                            code = "pipeleaf:shagblend-{ingredient1}-{ingredient2}"
                        }
                    }
                },
                transitionableProps = new[]
                {
                    new
                    {
                        type = "Perish",
                        freshHoursByType = new Dictionary<string, object>
                        {
                            ["*-cured"] = new { avg = 8640 },
                            ["*-shag"] = new { avg = 8640 },
                            ["*"] = new { avg = 336 }
                        },
                        transitionHours = new { avg = 12 },
                        transitionedStack = new
                        {
                            type = "item",
                            code = "game:rot"
                        },
                        transitionRatioByType = new Dictionary<string, int>
                        {
                            ["*"] = 1
                        }
                    }
                },
                nutritionProps = new
                {
                    satiety = 15,
                    foodcategory = "Vegetable"
                }
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.Never
            };

            var json = JsonSerializer.Serialize(output, options);
            File.WriteAllText(_config.OutputCuredBlendPath, json);
        }

        private void WriteCuredRecipesJson(List<Recipe> recipes)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.Never
            };

            var json = JsonSerializer.Serialize(recipes, options);
            File.WriteAllText(_config.OutputCuredRecipesPath, json);
        }

        private void WriteSmokableJson()
        {
            var attributesByType = new Dictionary<string, object>();

            foreach (var ingredient in _ingredientsConfig.Ingredients)
            {
                var key = $"smokable-{ingredient.Code}-shag";
                var attributes = new Dictionary<string, object>
                {
                    ["smokableEffects"] = ingredient.Effects
                };

                if (!string.IsNullOrEmpty(ingredient.HandbookDescription))
                {
                    attributes["handbook"] = new
                    {
                        groupBy = new[] { "smokable-*-shag" },
                        extraSections = new[]
                        {
                            new
                            {
                                title = "pipeleaf:handbook-item-effect",
                                text = ingredient.HandbookDescription
                            }
                        }
                    };
                }

                attributesByType[key] = attributes;
            }

            var output = new
            {
                code = "smokable",
                enabled = true,
                classByType = new Dictionary<string, string>
                {
                    ["*-shag"] = "SmokableItem"
                },
                creativeinventory = new
                {
                    general = new[] { "*" },
                    items = new[] { "*" }
                },
                materialDensity = 300,
                maxstacksize = 64,
                variantgroups = new object[]
                {
                    new { loadFromProperties = "pipeleaf:item/smokables" },
                    new
                    {
                        code = "type",
                        states = new[] { "cured", "shag" }
                    }
                },
                textureByType = new Dictionary<string, object>
                {
                    ["smokable-*-cured"] = new { @base = "pipeleaf:item/smokable/cured" },
                    ["smokable-*-shag"] = new { @base = "pipeleaf:item/smokable/shag" }
                },
                grindingPropsByType = new Dictionary<string, object>
                {
                    ["smokable-*-cured"] = new
                    {
                        GroundStack = new
                        {
                            type = "item",
                            stacksize = 1,
                            code = "smokable-{smokables}-shag"
                        }
                    }
                },
                attributesByType,
                transitionableProps = new[]
                {
                    new
                    {
                        type = "Perish",
                        freshHoursByType = new Dictionary<string, object>
                        {
                            ["*-cured"] = new { avg = 8640 },
                            ["*-shag"] = new { avg = 8640 },
                            ["*"] = new { avg = 336 }
                        },
                        transitionHours = new { avg = 12 },
                        transitionedStack = new
                        {
                            type = "item",
                            code = "game:rot"
                        },
                        transitionRatioByType = new Dictionary<string, int>
                        {
                            ["*"] = 1
                        }
                    }
                },
                nutritionProps = new
                {
                    satiety = 15,
                    foodcategory = "Vegetable"
                }
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.Never
            };

            var json = JsonSerializer.Serialize(output, options);
            File.WriteAllText(_config.SmokableJsonPath, json);
        }

        private void WriteWorldProperties()
        {
            // All ingredients for smokables.json (including excluded ones like pipeleaf)
            var allVariants = _ingredientsConfig.Ingredients
                .Select(i => new { code = i.Code })
                .ToList();

            // Only blendable ingredients (excluding pipeleaf, etc.)
            var blendableVariants = _ingredientsConfig.Ingredients
                .Where(i => !_config.ExcludeIngredients.Contains(i.Code))
                .Select(i => new { code = i.Code })
                .ToList();

            // Write smokables.json (all ingredients)
            var smokables = new
            {
                code = "smokables",
                variants = allVariants
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.Never
            };

            File.WriteAllText(_config.SmokablesJsonPath, JsonSerializer.Serialize(smokables, options));

            // Write blendable1.json (only blendable ingredients)
            var blendable1 = new
            {
                code = "ingredient1",
                variants = blendableVariants
            };

            File.WriteAllText(_config.OutputBlendable1Path, JsonSerializer.Serialize(blendable1, options));

            // Write blendable2.json (only blendable ingredients)
            var blendable2 = new
            {
                code = "ingredient2",
                variants = blendableVariants
            };

            File.WriteAllText(_config.OutputBlendable2Path, JsonSerializer.Serialize(blendable2, options));
        }
    }
}