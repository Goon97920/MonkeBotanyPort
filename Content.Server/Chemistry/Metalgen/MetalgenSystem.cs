using System.Linq;
using System.Text;
using System.Text.Json;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Metalgen;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Materials;
using Content.Shared.Mobs.Components;
using Content.Shared.Paper;
using Robust.Shared.ContentPack;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Chemistry.Metalgen;

/// <summary>
/// Monkestation metalgen.
/// <list type="bullet">
/// <item>Its recipe is random: 2 to 5 random medicines (1-10u each), a random temperature window,
/// and 1-10u of artifexium as a catalyst (wittel in Monkestation), making 20u. The recipe is
/// saved on the server and only changes every 3 days.</item>
/// <item>Mixing it with mercury and 40u or more of a metal imprints it with that metal.</item>
/// <item>Imprinted metalgen splashed on an object makes it out of that metal: what it's made of,
/// what it recycles into, its color and its name.</item>
/// <item>The "Old Recipe" paper shows the current recipe.</item>
/// </list>
/// </summary>
public sealed partial class MetalgenSystem : EntitySystem
{
    [Dependency] private ChemicalReactionSystem _reactions = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IResourceManager _res = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private PaperSystem _paper = default!;

    public static readonly ProtoId<ReactionPrototype> SynthesisReaction = "MetalgenSynthesis";
    public static readonly ProtoId<ReagentPrototype> Catalyst = "Artifexium";
    public static readonly ResPath RecipePath = new("/metalgen_recipe.json");

    /// <summary>Monkestation persistence_period: the recipe resets every 3 days.</summary>
    public static readonly TimeSpan RecipeLifetime = TimeSpan.FromDays(3);

    /// <summary>Material amount given to objects that weren't made of anything (Monkestation default_material_amount).</summary>
    public const int DefaultMaterialAmount = 100;

    private MetalgenRecipe? _recipe;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        SubscribeLocalEvent<TransformComponent, ReactionEntityEvent>(OnReactionEntity);
        SubscribeLocalEvent<MetalgenRecipePaperComponent, MapInitEvent>(OnRecipePaperMapInit);

        _recipe = LoadOrCreateRecipe();
        ApplyRecipe();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        // A reload replaces the prototype with its placeholder again.
        if (args.WasModified<ReactionPrototype>() || args.WasModified<ReagentPrototype>())
            ApplyRecipe();
    }

    #region Recipe

    public MetalgenRecipe? CurrentRecipe => _recipe;

    private MetalgenRecipe? LoadOrCreateRecipe()
    {
        if (_res.UserData.TryReadAllText(RecipePath, out var text))
        {
            try
            {
                var saved = JsonSerializer.Deserialize<MetalgenRecipe>(text);
                if (saved != null
                    && DateTime.UtcNow - saved.Created < RecipeLifetime
                    && saved.Reactants.Count > 0
                    && saved.Reactants.Keys.All(r => _proto.HasIndex<ReagentPrototype>(r))
                    && _proto.HasIndex<ReagentPrototype>(saved.Catalyst))
                {
                    return saved;
                }
            }
            catch (Exception e)
            {
                Log.Warning($"Couldn't read the saved metalgen recipe, making a new one: {e.Message}");
            }
        }

        var recipe = GenerateRecipe();
        if (recipe == null)
            return null;

        try
        {
            _res.UserData.WriteAllText(RecipePath, JsonSerializer.Serialize(recipe));
        }
        catch (Exception e)
        {
            Log.Warning($"Couldn't save the metalgen recipe: {e.Message}");
        }

        return recipe;
    }

    /// <summary>
    /// Monkestation /datum/chemical_reaction/randomized/metalgen GenerateRecipe.
    /// </summary>
    private MetalgenRecipe? GenerateRecipe()
    {
        // Medicines that chemists can actually make.
        var craftable = new HashSet<string>();
        foreach (var reaction in _proto.EnumeratePrototypes<ReactionPrototype>())
        {
            if (reaction.ID == SynthesisReaction)
                continue;
            foreach (var product in reaction.Products.Keys)
                craftable.Add(product);
        }

        var medicines = _proto.EnumeratePrototypes<ReagentPrototype>()
            .Where(r => !r.Abstract && r.Group == "Medicine" && craftable.Contains(r.ID) && r.ID != Catalyst)
            .Select(r => r.ID)
            .OrderBy(r => r)
            .ToList();

        if (medicines.Count < 2)
        {
            Log.Error("Not enough craftable medicines to make a metalgen recipe.");
            return null;
        }

        var recipe = new MetalgenRecipe
        {
            Created = DateTime.UtcNow,
            Catalyst = Catalyst,
            CatalystAmount = _random.Next(1, 11),
        };

        var count = Math.Min(_random.Next(2, 6), medicines.Count);
        for (var i = 0; i < count; i++)
        {
            var pick = _random.PickAndTake(medicines);
            recipe.Reactants[pick] = _random.Next(1, 11);
        }

        // Monkestation randomize_req_temperature with min_temp 1 and max_temp 600.
        const int minTemp = 1;
        const int maxTemp = 600;
        recipe.Cold = _random.Prob(0.5f);
        if (recipe.Cold)
        {
            var required = _random.Next(minTemp + 50, maxTemp + 1);
            var optimal = _random.Next(minTemp + 25, Math.Max(minTemp + 25, required - 10) + 1);
            var overheat = _random.Next(minTemp, Math.Max(minTemp, optimal - 10) + 1);
            recipe.RequiredTemperature = required;
            recipe.LimitTemperature = Math.Min(overheat, 200);
        }
        else
        {
            var required = _random.Next(minTemp, maxTemp - 50 + 1);
            var optimal = _random.Next(required + 10, Math.Max(required + 10, maxTemp - 25) + 1);
            var overheat = _random.Next(optimal, maxTemp + 50 + 1);
            recipe.RequiredTemperature = required;
            recipe.LimitTemperature = Math.Max(overheat, 400);
        }

        return recipe;
    }

    /// <summary>
    /// Writes the recipe into the server's copy of the placeholder reaction. Clients keep the
    /// placeholder, which can never react, and the reaction is hidden from the guidebook.
    /// </summary>
    private void ApplyRecipe()
    {
        if (_recipe == null || !_proto.TryIndex(SynthesisReaction, out var reaction))
            return;

        reaction.Reactants.Clear();
        foreach (var (reagent, amount) in _recipe.Reactants)
        {
            reaction.Reactants[reagent] = new ReactantInfo(amount, false);
        }

        reaction.Reactants[_recipe.Catalyst] = new ReactantInfo(_recipe.CatalystAmount, true);
        reaction.Quantized = false;
        if (_recipe.Cold)
        {
            reaction.MaximumTemperature = _recipe.RequiredTemperature;
            reaction.MinimumTemperature = _recipe.LimitTemperature;
        }
        else
        {
            reaction.MinimumTemperature = _recipe.RequiredTemperature;
            reaction.MaximumTemperature = _recipe.LimitTemperature;
        }

        _reactions.RebuildReactionCache();
    }

    /// <summary>Monkestation /obj/item/paper/secretrecipe UpdateInfo.</summary>
    public string GetRecipeText()
    {
        if (_recipe == null)
            return Loc.GetString("metalgen-recipe-illegible");

        var sb = new StringBuilder();
        sb.AppendLine(Loc.GetString("metalgen-recipe-header"));
        foreach (var (reagent, amount) in _recipe.Reactants)
        {
            sb.AppendLine(Loc.GetString("metalgen-recipe-line", ("amount", amount), ("reagent", ReagentName(reagent))));
        }

        sb.AppendLine(Loc.GetString("metalgen-recipe-catalyst"));
        sb.AppendLine(Loc.GetString("metalgen-recipe-line", ("amount", _recipe.CatalystAmount), ("reagent", ReagentName(_recipe.Catalyst))));
        sb.AppendLine(Loc.GetString("metalgen-recipe-mix"));
        sb.AppendLine(Loc.GetString(_recipe.Cold ? "metalgen-recipe-cold" : "metalgen-recipe-hot",
            ("required", _recipe.RequiredTemperature),
            ("limit", _recipe.LimitTemperature)));
        return sb.ToString();
    }

    private string ReagentName(string id) =>
        _proto.TryIndex<ReagentPrototype>(id, out var proto) ? proto.LocalizedName : id;

    private void OnRecipePaperMapInit(Entity<MetalgenRecipePaperComponent> ent, ref MapInitEvent args)
    {
        if (TryComp<PaperComponent>(ent, out var paper))
            _paper.SetContent((ent, paper), GetRecipeText());
    }

    #endregion

    #region Transmutation

    /// <summary>Splashed, sprayed or smoked onto an object.</summary>
    private void OnReactionEntity(Entity<TransformComponent> ent, ref ReactionEntityEvent args)
    {
        if (args.Method != ReactionMethod.Touch
            || args.Reagent.ID != "Metalgen"
            || GetMaterial(args.ReagentQuantity.Reagent) is not { } material)
            return;

        Transmute(ent, material);
    }

    public static ProtoId<MaterialPrototype>? GetMaterial(ReagentId reagent)
    {
        if (reagent.Data == null)
            return null;

        foreach (var data in reagent.Data)
        {
            if (data is MetalgenData metalgen)
                return metalgen.Material;
        }

        return null;
    }

    /// <summary>
    /// Monkestation metal_morph: the object becomes made entirely of the material, keeping its
    /// total amount (or 100 if it had none), and takes the material's color and name.
    /// </summary>
    public void Transmute(EntityUid uid, ProtoId<MaterialPrototype> materialId)
    {
        if (!_proto.TryIndex(materialId, out var material)
            || HasComp<MobStateComponent>(uid)
            || HasComp<MapGridComponent>(uid)
            || HasComp<MapComponent>(uid)
            || TerminatingOrDeleted(uid))
            return;

        var composition = EnsureComp<PhysicalCompositionComponent>(uid);
        var total = composition.MaterialComposition.Values.Sum();
        if (total <= 0)
            total = DefaultMaterialAmount;

        composition.MaterialComposition.Clear();
        composition.MaterialComposition[materialId] = total;

        var transmuted = EnsureComp<MetalgenTransmutedComponent>(uid);
        transmuted.OriginalName ??= Name(uid);
        transmuted.Material = materialId;
        transmuted.Color = material.Color;
        Dirty(uid, transmuted);

        _metaData.SetEntityName(uid, Loc.GetString("metalgen-transmuted-name",
            ("material", Loc.GetString(material.Name)),
            ("name", transmuted.OriginalName)));
    }

    #endregion
}

/// <summary>The saved random recipe.</summary>
public sealed class MetalgenRecipe
{
    public DateTime Created { get; set; }
    public Dictionary<string, int> Reactants { get; set; } = new();
    public string Catalyst { get; set; } = "Artifexium";
    public int CatalystAmount { get; set; } = 1;

    /// <summary>Cold recipes react below <see cref="RequiredTemperature"/> but not below <see cref="LimitTemperature"/>.</summary>
    public bool Cold { get; set; }

    public float RequiredTemperature { get; set; }
    public float LimitTemperature { get; set; }
}
