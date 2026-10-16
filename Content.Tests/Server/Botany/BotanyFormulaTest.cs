using Content.Server.Botany.Systems;
using Content.Shared.Botany;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Prototypes;
using NUnit.Framework;

namespace Content.Tests.Server.Botany;

/// <summary>
/// The Monkestation botany formulas, checked against values worked out by hand from the DM code.
/// </summary>
[TestFixture]
[TestOf(typeof(BotanySystem))]
public sealed class BotanyFormulaTest
{
    [Test]
    [TestCase(0f, 0)]
    [TestCase(0.5f, 0)]
    [TestCase(1f, 1)] // Monkestation gives 0 below 10 yield; SS14 always harvests something
    [TestCase(9f, 1)]
    [TestCase(10f, 1)]
    [TestCase(40f, 4)]
    [TestCase(99f, 9)]
    [TestCase(100f, 10)] // 20 * 100 / 200
    [TestCase(300f, 15)] // 20 * 300 / 400
    [TestCase(1e6f, 19)]
    public void HarvestAmount(float yield, int expected)
    {
        Assert.That(BotanySystem.GetHarvestAmount(yield), Is.EqualTo(expected));
    }

    [Test]
    public void HarvestAmountCapsAtTwenty()
    {
        Assert.That(BotanySystem.GetHarvestAmount(PlantGenome.MaxStat), Is.LessThanOrEqualTo(20));
    }

    /// <summary>Hypodermic prickles: 50 * P / (2000 + P), the same for throws and slips.</summary>
    [Test]
    [TestCase(0f, 0f)]
    [TestCase(2000f, 25f)]
    [TestCase(6000f, 37.5f)]
    public void PricklesInjection(float potency, float expected)
    {
        Assert.That(BotanySystem.QpSigmoid(2000f, 50f, potency), Is.EqualTo(expected).Within(0.001f));
    }

    [Test]
    [TestCase(10f, 100f, 2f)]   // max(0.5, 1) * 1 = 1, +1
    [TestCase(100f, 100f, 6f)]  // 5 * 1 = 5, +1
    [TestCase(100f, 420f, 22f)] // 5 * 4.2 = 21, +1
    [TestCase(1000f, 840f, 421f)]
    public void BiteSize(float potency, float volume, float expected)
    {
        Assert.That(PlantTraitSystem.GetBiteSize(potency, volume), Is.EqualTo(expected));
    }

    [Test]
    public void ManipulatorDegradesToTierFourCaps()
    {
        var comp = new PlantDnaManipulatorComponent();
        Assert.Multiple(() =>
        {
            Assert.That(PlantDnaManipulatorSystem.DegradeCoreGene(comp, PlantStat.Potency, 5000f), Is.EqualTo(5000f));
            Assert.That(PlantDnaManipulatorSystem.DegradeCoreGene(comp, PlantStat.Yield, 60f), Is.EqualTo(10f));
            Assert.That(PlantDnaManipulatorSystem.DegradeCoreGene(comp, PlantStat.Endurance, 500f), Is.EqualTo(110f));
            Assert.That(PlantDnaManipulatorSystem.DegradeCoreGene(comp, PlantStat.Lifespan, 500f), Is.EqualTo(110f));
            Assert.That(PlantDnaManipulatorSystem.DegradeCoreGene(comp, PlantStat.WeedRate, -5f), Is.EqualTo(0f));
            Assert.That(PlantDnaManipulatorSystem.DegradeCoreGene(comp, PlantStat.WeedChance, 0f), Is.EqualTo(3f));
            Assert.That(PlantDnaManipulatorSystem.DegradeCoreGene(comp, PlantStat.Production, 1f), Is.EqualTo(1f));
        });
    }

    [Test]
    public void GenomeClampsStats()
    {
        var genome = new PlantGenome { Species = "WheatPlants" };
        genome.SetStat(PlantStat.WeedRate, 50f);
        genome.SetStat(PlantStat.WeedChance, 500f);
        genome.SetStat(PlantStat.Endurance, 1f);
        genome.SetStat(PlantStat.Lifespan, -3f);
        genome.SetStat(PlantStat.Production, 0f);
        genome.SetStat(PlantStat.Potency, float.MaxValue);
        Assert.Multiple(() =>
        {
            Assert.That(genome.WeedRate, Is.EqualTo(PlantGenome.MaxWeedRate));
            Assert.That(genome.WeedChance, Is.EqualTo(PlantGenome.MaxWeedChance));
            Assert.That(genome.Endurance, Is.EqualTo(PlantGenome.MinEndurance));
            Assert.That(genome.Lifespan, Is.EqualTo(PlantGenome.MinLifespan));
            Assert.That(genome.Production, Is.EqualTo(1f));
            Assert.That(genome.Potency, Is.EqualTo(PlantGenome.MaxStat));
        });
    }

    [Test]
    [TestCase("pumpkin", "redbeet", "pumpkibeet")]
    public void SpliceName(string a, string b, string expected)
    {
        Assert.That(PlantSplicerSystem.SpliceName(a, b), Is.EqualTo(expected));
    }

    /// <summary>SS14's old wheat flour: 5 + potency / 20, at most 20.</summary>
    [Test]
    [TestCase(15f, 5.75f)]
    [TestCase(100f, 10f)]
    [TestCase(1000f, 20f)]
    public void CookingMinimum(float potency, float expected)
    {
        var minimum = new PlantCookingMinimum { Min = 5, Max = 20, PotencyDivisor = 20 };
        Assert.That(minimum.GetAmount(potency), Is.EqualTo(expected).Within(0.001f));
    }
}
