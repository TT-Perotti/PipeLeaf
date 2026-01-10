using System;
using System.Collections.Generic;
using System.IO;
using Cake.Common;
using Cake.Common.Diagnostics;
using Cake.Common.IO;
using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.Clean;
using Cake.Common.Tools.DotNet.Publish;
using Cake.Core;
using Cake.Core.IO;
using Cake.Frosting;
using Cake.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using PipeLeaf;


public static class Program
{
    public static int Main(string[] args)
    {
        return new CakeHost()
            .UseContext<BuildContext>()
            .Run(args);
    }
}

public class BuildContext : FrostingContext
{
    public const string ProjectName = "PipeLeaf";
    public string BuildConfiguration { get; set; }
    public string Version { get; }
    public string Name { get; }
    public bool SkipJsonValidation { get; set; }

    public BuildContext(ICakeContext context)
        : base(context)
    {
        BuildConfiguration = context.Argument("configuration", "Release");
        SkipJsonValidation = context.Argument("skipJsonValidation", false);
        var modInfo = context.DeserializeJsonFromFile<ModInfo>($"../{BuildContext.ProjectName}/modinfo.json");
        Version = modInfo.Version;
        Name = modInfo.ModID;
    }
}

[TaskName("GenerateBlends")]
public sealed class GenerateBlendsTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        context.Information("Generating shagblend definitions and recipes...");

        var smokableJsonPath = $"../{BuildContext.ProjectName}/assets/pipeleaf/itemtypes/smokable.json";
        var smokablesJsonPath = $"../{BuildContext.ProjectName}/assets/pipeleaf/worldproperties/item/smokables.json";
        var outputShagblendPath = $"../{BuildContext.ProjectName}/assets/pipeleaf/itemtypes/shagblend.json";
        var outputRecipesPath = $"../{BuildContext.ProjectName}/assets/pipeleaf/recipes/grid/shagblend-recipes.json";
        var outputLangPath = $"../{BuildContext.ProjectName}/assets/pipeleaf/lang/en.json";
        var outputCuredBlendPath = $"../{BuildContext.ProjectName}/assets/pipeleaf/itemtypes/cured-blends.json";
        var outputCuredRecipesPath = $"../{BuildContext.ProjectName}/assets/pipeleaf/recipes/grid/cured-blends.json";
        var ingredientsConfigPath = $"../{BuildContext.ProjectName}/assets/pipeleaf/config/ingredients-config.json";
        var outputPatchesPath = $"../{BuildContext.ProjectName}/assets/pipeleaf/patches/allow-plant-curing.json";

        // Define special blends - 15 total (5 vanilla-only, 5 mixed, 5 wildcraft-only)
        var specialBlends = new List<SpecialBlend>
        {
            // === FROM BOTH LISTS (5 blends) ===
            
            // 1. Dream Walker - Vanilla-only, sleep + temporal boost
            new SpecialBlend
            {
                Ingredient1 = "edelweiss",
                Ingredient2 = "goldenpoppy",
                FlavorName = "Dream Walker",
                Description = "A rare blend that promotes deep, restful sleep while maintaining temporal stability. Perfect for safe rest in dangerous territories.",
                AdditionalProperties = new Dictionary<string, object>
                {
                    ["maxstacksize"] = 16
                }
            },
            
            // 2. Madman's Slumber - Vanilla-only, risky sleep aid
            new SpecialBlend
            {
                Ingredient1 = "catmint",
                Ingredient2 = "goldenpoppy",
                FlavorName = "Madman's Slumber",
                Description = "Induces powerful drowsiness while slightly attracting temporal rifts. For those desperate enough to risk nightmares for sleep.",
                AdditionalProperties = new Dictionary<string, object>
                {
                    ["maxstacksize"] = 32
                }
            },
            
            // 3. Death's Door - Most dangerous blend
            new SpecialBlend
            {
                Ingredient1 = "catmint",
                Ingredient2 = "poisonoak",
                FlavorName = "Death's Door",
                Description = "EXTREMELY DANGEROUS. Causes immediate health damage and severely disrupts temporal stability. Smoking this is tantamount to inviting death itself.",
                AdditionalProperties = new Dictionary<string, object>
                {
                    ["maxstacksize"] = 8
                }
            },
            
            // 4. Fool's Last Meal - Deadly with appetite suppression
            new SpecialBlend
            {
                Ingredient1 = "orangemallow",
                Ingredient2 = "poisonoak",
                FlavorName = "Fool's Last Meal",
                Description = "A lethal combination that somehow suppresses hunger. You won't feel hungry... because you'll be too busy dying. Not recommended.",
                AdditionalProperties = new Dictionary<string, object>
                {
                    ["maxstacksize"] = 8
                }
            },
            
            // 5. Sweet Stupor - Recreational blend
            new SpecialBlend
            {
                Ingredient1 = "orangemallow",
                Ingredient2 = "marshmallow",
                FlavorName = "Sweet Stupor",
                Description = "A pleasant, mildly intoxicating blend that also curbs hunger. Popular in taverns for its balance of enjoyment and utility.",
                AdditionalProperties = new Dictionary<string, object>
                {
                    ["maxstacksize"] = 64
                }
            },
            
            // === VANILLA-ONLY EXCLUSIVES (5 blends) ===
            
            // 6. Alpine Haze - Best balanced vanilla blend
            new SpecialBlend
            {
                Ingredient1 = "edelweiss",
                Ingredient2 = "orangemallow",
                FlavorName = "Alpine Haze",
                Description = "Rare mountain edelweiss combined with orange mallow creates a stabilizing yet intoxicating blend. Popular among climbers and deep miners.",
                AdditionalProperties = new Dictionary<string, object>
                {
                    ["maxstacksize"] = 24
                }
            },
            
            // 7. Meadow's Fast - Best vanilla appetite suppressant
            new SpecialBlend
            {
                Ingredient1 = "orangemallow",
                Ingredient2 = "cornflower",
                FlavorName = "Meadow's Fast",
                Description = "A practical travel blend combining appetite suppression with mild intoxication. Common among long-distance traders.",
                AdditionalProperties = new Dictionary<string, object>
                {
                    ["maxstacksize"] = 48
                }
            },
            
            // 8. Mountain Warmth - Simple temporal boost
            new SpecialBlend
            {
                Ingredient1 = "edelweiss",
                Ingredient2 = "cornflower",
                FlavorName = "Mountain Warmth",
                Description = "Rare edelweiss provides temporal stability while cornflower adds comforting warmth. A simple but effective combination.",
                AdditionalProperties = new Dictionary<string, object>
                {
                    ["maxstacksize"] = 24
                }
            },
            
            // 9. Fool's Courage - Tavern favorite
            new SpecialBlend
            {
                Ingredient1 = "catmint",
                Ingredient2 = "orangemallow",
                FlavorName = "Fool's Courage",
                Description = "Mildly intoxicating but destabilizing. Popular in taverns despite the slight temporal risk. 'Liquid courage' for the reckless.",
                AdditionalProperties = new Dictionary<string, object>
                {
                    ["maxstacksize"] = 48
                }
            },
            
            // 10. Sunset Dreams - Pleasant evening blend
            new SpecialBlend
            {
                Ingredient1 = "orangemallow",
                Ingredient2 = "goldenpoppy",
                FlavorName = "Sunset Dreams",
                Description = "Makes you drowsy and slightly intoxicated. The perfect 'end of day' blend for winding down after hard work.",
                AdditionalProperties = new Dictionary<string, object>
                {
                    ["maxstacksize"] = 48
                }
            },
            
            // === TOP-10 EXCLUSIVES (5 blends, require Wildcraft) ===
            
            // 11. Drifter's Demise - STRONGEST temporal stability
            new SpecialBlend
            {
                Ingredient1 = "sage",
                Ingredient2 = "thyme",
                FlavorName = "Drifter's Demise",
                Description = "The most powerful temporal stability blend known. Essential for deep delving into temporal storms, though the heavy intoxication may impair your judgment.",
                AdditionalProperties = new Dictionary<string, object>
                {
                    ["maxstacksize"] = 32
                }
            },
            
            // 12. Scholar's Vision - Second-best temporal
            new SpecialBlend
            {
                Ingredient1 = "edelweiss",
                Ingredient2 = "sage",
                FlavorName = "Scholar's Vision",
                Description = "A rare and potent blend combining mountain edelweiss with sacred sage. Grants profound temporal clarity, though at the cost of severe intoxication.",
                AdditionalProperties = new Dictionary<string, object>
                {
                    ["maxstacksize"] = 16
                }
            },
            
            // 13. Traveler's Fast - Best appetite suppressant
            new SpecialBlend
            {
                Ingredient1 = "orangemallow",
                Ingredient2 = "marjoram",
                FlavorName = "Traveler's Fast",
                Description = "A powerful double appetite suppressant, perfect for long journeys when rations are scarce. The drowsiness is a small price to pay for sustained travel.",
                AdditionalProperties = new Dictionary<string, object>
                {
                    ["maxstacksize"] = 48
                }
            },
            
            // 14. Chaos Theory - Contradictory effects
            new SpecialBlend
            {
                Ingredient1 = "sage",
                Ingredient2 = "catmint",
                FlavorName = "Chaos Theory",
                Description = "A paradoxical blend where stabilizing sage meets destabilizing catmint. The result is... unpredictable clarity through confusion.",
                AdditionalProperties = new Dictionary<string, object>
                {
                    ["maxstacksize"] = 32
                }
            },
            
            // 15. Hermit's Evening - Bedtime blend
            new SpecialBlend
            {
                Ingredient1 = "chamomile",
                Ingredient2 = "marjoram",
                FlavorName = "Hermit's Evening",
                Description = "The perfect end-of-day smoke. Promotes restful sleep while suppressing late-night hunger pangs. A favorite among solitary travelers.",
                AdditionalProperties = new Dictionary<string, object>
                {
                    ["maxstacksize"] = 64
                }
            }
        };

        // Call the generator directly
        var config = new BlendGeneratorConfig
        {
            SmokableJsonPath = smokableJsonPath,
            SmokablesJsonPath = smokablesJsonPath,
            OutputShagblendPath = outputShagblendPath,
            OutputRecipesPath = outputRecipesPath,
            OutputLangPath = outputLangPath,
            OutputCuredBlendPath = outputCuredBlendPath,
            OutputCuredRecipesPath = outputCuredRecipesPath,
            GenerateFromConfig = true,
            IngredientsConfigPath = ingredientsConfigPath,
            OutputBlendable1Path = $"../{BuildContext.ProjectName}/assets/pipeleaf/worldproperties/item/blendable1.json",
            OutputBlendable2Path = $"../{BuildContext.ProjectName}/assets/pipeleaf/worldproperties/item/blendable2.json",
            OutputPatchesPath = outputPatchesPath,
            SpecialBlends = specialBlends
        };

        var generator = new ShagBlendGenerator(config);
        generator.Generate();

        context.Information("Blend generation complete!");
    }
}

[TaskName("ValidateJson")]
[IsDependentOn(typeof(GenerateBlendsTask))]
public sealed class ValidateJsonTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        if (context.SkipJsonValidation)
        {
            return;
        }
        var jsonFiles = context.GetFiles($"../{BuildContext.ProjectName}/assets/**/*.json");
        foreach (var file in jsonFiles)
        {
            try
            {
                var json = File.ReadAllText(file.FullPath);
                JToken.Parse(json);
            }
            catch (JsonException ex)
            {
                throw new Exception($"Validation failed for JSON file: {file.FullPath}{Environment.NewLine}{ex.Message}", ex);
            }
        }
    }
}

[TaskName("Build")]
[IsDependentOn(typeof(ValidateJsonTask))]
public sealed class BuildTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        context.DotNetClean($"../{BuildContext.ProjectName}/{BuildContext.ProjectName}.csproj",
            new DotNetCleanSettings
            {
                Configuration = context.BuildConfiguration
            });


        context.DotNetPublish($"../{BuildContext.ProjectName}/{BuildContext.ProjectName}.csproj",
            new DotNetPublishSettings
            {
                Configuration = context.BuildConfiguration
            });
    }
}

[TaskName("Package")]
[IsDependentOn(typeof(BuildTask))]
public sealed class PackageTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        context.EnsureDirectoryExists("../Releases");
        context.CleanDirectory("../Releases");
        context.EnsureDirectoryExists($"../Releases/{context.Name}");
        context.CopyFiles($"../{BuildContext.ProjectName}/bin/{context.BuildConfiguration}/Mods/mod/publish/*", $"../Releases/{context.Name}");
        context.CopyDirectory($"../{BuildContext.ProjectName}/assets", $"../Releases/{context.Name}/assets");
        context.CopyFile($"../{BuildContext.ProjectName}/modinfo.json", $"../Releases/{context.Name}/modinfo.json");

        context.CopyFile($"../{BuildContext.ProjectName}/modicon.png", $"../Releases/{context.Name}/modicon.png");
        context.Zip($"../Releases/{context.Name}", $"../Releases/{context.Name}_{context.Version}.zip");
    }
}

[TaskName("Default")]
[IsDependentOn(typeof(PackageTask))]
public class DefaultTask : FrostingTask
{
}