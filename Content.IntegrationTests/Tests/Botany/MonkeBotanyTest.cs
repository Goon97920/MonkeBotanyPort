using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Botany.Systems;
using Content.Server.Chemistry.Metalgen;
using Content.Shared.Atmos;
using Content.Shared.Botany;
using Content.Shared.Botany.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Materials;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Botany;

/// <summary>
/// Checks the Monkestation botany port end to end on a test map.
/// </summary>
[TestFixture]
public sealed class MonkeBotanyTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Dirty = true };

    /// <summary>
    /// A thousand lesser strange seeds, the way botanists make them: each gets its own random genome,
    /// they all land on one tile, and the extractor stores and returns them as data.
    /// </summary>
    [Test]
    public async Task StrangeSeedBatch()
    {
        var pair = Pair;
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;
        var botany = entMan.System<BotanySystem>();

        var seeds = new List<EntityUid>();
        double spawnMs = 0;
        await server.WaitPost(() =>
        {
            var watch = Stopwatch.StartNew();
            for (var i = 0; i < 1000; i++)
            {
                seeds.Add(entMan.SpawnEntity("LesserStrangeSeeds", map.GridCoords));
            }

            spawnMs = watch.Elapsed.TotalMilliseconds;
        });

        await server.WaitRunTicks(5);
        TestContext.Out.WriteLine($"Spawned 1000 lesser strange seeds in {spawnMs:0.0} ms");

        await server.WaitAssertion(() =>
        {
            var genomes = seeds.Select(s => entMan.GetComponent<SeedComponent>(s).Genome).ToList();
            Assert.That(genomes, Has.All.Not.Null);
            // Random genomes: not all identical.
            Assert.That(genomes.Select(g => (g!.Potency, g.Yield, g.Lifespan)).Distinct().Count(), Is.GreaterThan(10));

            // The lesser blacklist holds.
            var blacklist = new[] { "Stimulants", "Omnizine", "Metalgen", "Healium" };
            Assert.That(genomes.SelectMany(g => g!.Chemicals).Select(c => c.Reagent.Id).Intersect(blacklist), Is.Empty);

            // Seedify puts a whole batch on one spot.
            var batch = botany.Seedify(genomes[0]!, map.GridCoords, 4);
            Assert.That(batch, Has.Count.InRange(5, 17));
            var positions = batch.Select(s => entMan.GetComponent<TransformComponent>(s).LocalPosition).Distinct().ToList();
            Assert.That(positions, Has.Count.EqualTo(1));
            foreach (var s in batch)
            {
                entMan.DeleteEntity(s);
            }
        });

        await server.WaitPost(() =>
        {
            foreach (var s in seeds)
            {
                entMan.DeleteEntity(s);
            }
        });
    }

    /// <summary>Harvest amount, the chemical formula and the cooking-reagent floor on real prototypes.</summary>
    [Test]
    public async Task ProduceChemicals()
    {
        var pair = Pair;
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;
        var botany = entMan.System<BotanySystem>();
        var solutions = entMan.System<SharedSolutionContainerSystem>();

        await server.WaitAssertion(() =>
        {
            var genome = botany.CreateGenome("WheatPlants")!;
            Assert.That(genome, Is.Not.Null);

            // Yield 1 still harvests something.
            genome.SetStat(PlantStat.Yield, 1);
            var harvest = botany.Harvest(genome, map.GridCoords);
            Assert.That(harvest, Is.Not.Empty);
            foreach (var e in harvest)
            {
                entMan.DeleteEntity(e);
            }

            // Wheat at potency 15: Monkestation gives 1u of flour, SS14's old botany gave 5.75u.
            genome.SetStat(PlantStat.Potency, 15);
            var bushel = botany.SpawnProduce("WheatBushel", genome, map.GridCoords);
            Assert.That(solutions.TryGetSolution(bushel, "food", out _, out var sol));
            Assert.That(sol!.GetTotalPrototypeQuantity("Flour").Float(), Is.EqualTo(5.75f).Within(0.01f));
            entMan.DeleteEntity(bushel);

            // Pumpkin with densified chemicals holds twice as much.
            var pumpkin = botany.CreateGenome("PumpkinPlants")!;
            pumpkin.SetStat(PlantStat.Potency, 100);
            var plain = botany.SpawnProduce("FoodPumpkin", pumpkin, map.GridCoords);
            pumpkin.Traits.Add(new PlantTraitGene("DensifiedChemicals", PlantGeneFlags.Removable));
            var dense = botany.SpawnProduce("FoodPumpkin", pumpkin, map.GridCoords);
            Assert.That(solutions.TryGetSolution(plain, "food", out _, out var plainSol));
            Assert.That(solutions.TryGetSolution(dense, "food", out _, out var denseSol));
            Assert.That(denseSol!.MaxVolume, Is.EqualTo(plainSol!.MaxVolume * 2));
            entMan.DeleteEntity(plain);
            entMan.DeleteEntity(dense);
        });
    }

    /// <summary>The seed extractor keeps hand-inserted seeds as data and gives them back.</summary>
    [Test]
    public async Task ExtractorStorage()
    {
        var pair = Pair;
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;
        var botany = entMan.System<BotanySystem>();

        await server.WaitAssertion(() =>
        {
            var extractor = entMan.SpawnEntity("SeedExtractor", map.GridCoords);
            var comp = entMan.GetComponent<SeedExtractorComponent>(extractor);
            Assert.That(comp.MaxSeeds, Is.EqualTo(4000));
            Assert.That(comp.StoredCount, Is.Zero);

            // Seeds made by extraction land on the floor, never in storage.
            var genome = botany.CreateGenome("TomatoPlants")!;
            var made = botany.Seedify(genome, map.GridCoords, 4);
            Assert.That(made, Is.Not.Empty);
            Assert.That(comp.StoredCount, Is.Zero);
            foreach (var s in made)
            {
                entMan.DeleteEntity(s);
            }

            entMan.DeleteEntity(extractor);
        });
    }

    [Test]
    public async Task GasesAndMetalgen()
    {
        var pair = Pair;
        var server = pair.Server;
        var proto = server.ProtoMan;
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            Assert.That(Atmospherics.TotalNumberOfGases, Is.EqualTo(11));
            Assert.That(proto.HasIndex<Content.Shared.Atmos.Prototypes.GasPrototype>("BZ"));
            Assert.That(proto.HasIndex<Content.Shared.Atmos.Prototypes.GasPrototype>("Healium"));

            // The server holds the real metalgen recipe: medicines, an artifexium catalyst, 20u out.
            var metalgen = entMan.System<MetalgenSystem>();
            var recipe = metalgen.CurrentRecipe;
            Assert.That(recipe, Is.Not.Null);
            Assert.That(recipe!.Reactants, Has.Count.InRange(2, 5));
            Assert.That(recipe.Catalyst, Is.EqualTo("Artifexium"));

            var reaction = proto.Index<ReactionPrototype>(MetalgenSystem.SynthesisReaction);
            Assert.That(reaction.Hidden);
            Assert.That(reaction.Quantized, Is.False);
            Assert.That(reaction.Reactants.Keys.Select(k => k.Id), Does.Contain("Artifexium"));
            Assert.That(reaction.Products["Metalgen"].Float(), Is.EqualTo(20f));
            Assert.That(metalgen.GetRecipeText(), Does.Contain(proto.Index<Content.Shared.Chemistry.Reagent.ReagentPrototype>("Artifexium").LocalizedName));
        });
    }

    /// <summary>Imprinted metalgen turns an object into the metal: composition, name and color.</summary>
    [Test]
    public async Task MetalgenTransmutes()
    {
        var pair = Pair;
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var crowbar = entMan.SpawnEntity("Crowbar", map.GridCoords);
            var oldName = entMan.GetComponent<MetaDataComponent>(crowbar).EntityName;
            entMan.System<MetalgenSystem>().Transmute(crowbar, "Gold");

            var composition = entMan.GetComponent<PhysicalCompositionComponent>(crowbar);
            Assert.That(composition.MaterialComposition.Keys.Select(k => k.Id), Is.EquivalentTo(new[] { "Gold" }));
            Assert.That(entMan.GetComponent<MetaDataComponent>(crowbar).EntityName, Does.Contain(oldName));
            Assert.That(entMan.HasComponent<Content.Shared.Chemistry.Metalgen.MetalgenTransmutedComponent>(crowbar));
            entMan.DeleteEntity(crowbar);
        });
    }
}
