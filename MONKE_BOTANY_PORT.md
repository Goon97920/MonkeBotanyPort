# Monkestation botany for Space Station 14

This branch replaces SS14 botany with Monkestation 2.0's (master, commit 70c2e7a0), plus the
extras agreed for this server. SS14 species, sprites and prototype IDs are kept, so existing
seeds, produce, maps and recipes still work.

## What's in it

**Genetics**
- Seeds carry a genome:
  - Core genes: potency, yield, production, endurance, lifespan, weed rate, weed chance, maturation.
  - Chemical genes: a reagent and a rate.
  - Trait genes, each with removable, mutatable and graftable flags. A trait with none of these is Immutable.
- Stats are effectively uncapped (1e31). Weed rate is capped at 10 and weed chance at 67.
- Produce chemicals:
  - Base volume is 100u, adjusted by volume traits; Densified Chemicals doubles it.
  - Each chemical gets volume × potency/100 × rate, scaled down to fit, with a minimum of 1u.
  - Cooking reagents never come out below what SS14's old botany gave (min + potency ÷ divisor, up to its cap). This covers flour, cornmeal, rice, oats, egg, milk, sugar, enzyme, raw meat, frost oil, capsaicin, allicin, tomato juice, fiber and pumpkin flesh.
- Harvest:
  - Below 100 yield, the item count is yield × 0.1. From 100 up it is 20 × yield / (100 + yield).
  - About 20% of the items come out as seeds.
  - **SS14 change:** any yield of 1 or more harvests at least one item.
- Mutations: when a plant's stats are inside a species' window, 65% of its seeds and 10% of its produce become the mutated species.

**Machines and tools**
- **Seed extractor:**
  - Produce in, rand(1,4) × 4 + 1 seeds out, dropped where you click.
  - Seeds you put in by hand or from a bag are stored as data (up to 4000, in piles) and taken back out through its UI.
  - Seeds the extractor makes never go into its storage.
- **Plant DNA manipulator:** extract genes onto disks (this destroys the seed), remove genes, insert them, and replace core genes. It uses tier-4 limits: potency uncapped, yield 10, endurance and lifespan 110, weed rate 0, weed chance 3.
- **Seed splicer:**
  - Splices two seeds into a hybrid.
  - Infuses a seed with a beaker of reagents: stat changes from uranium, radium, ammonia, Robust Harvest and saltpetre. 100u or more of a reagent becomes a gene. Infusion damages the seed.
- **Grafting** with plant clippers (secateurs), and **trait training** from tray reagents.
- **Biogenerator:**
  - Nutriment, vitamin and protein all count as biomass, ×4 (tier-4 productivity).
  - Holds up to 1e9 biomass.
  - Lesser strange seeds cost 500 biomass (2000 at tier-4 efficiency). Emagged, it also prints regular strange seeds.
  - Everything else costs the same as in SS14.
  - A plant bag in its output slot catches printed seeds.
- **Botanist's locker:** gets a box of 6 plant data disks and a portable seed extractor. Use alt-click to turn the plants inside into seeds.
- Plant data disks are a roundstart print at the service techfab. The manipulator and splicer boards unlock with Hydroponics research.

**Traits**
- All 48 Monkestation traits.
- Hypodermic Prickles injects 50·P/(2000+P) when the plant is thrown or slipped on.
- Prickly Adhesion embeds when thrown. If the plant also has Hypodermic Prickles, it injects 10u every 5 s while embedded; this stacks.
- Syndicate Hypodermic Needles injects through hardsuits and armor, in melee only.
- Large Bites eats the whole thing. Whatever doesn't fit in the stomach goes to a non-reacting secondary stomach, which empties into the stomach as space frees up.

**Uplink (botanists only)**
- Catalytic Inhibitor Serum disk: 17 TC.
- Syndicate Hypodermic Needles disk: 10 TC.

**Medical**
- Stomach pump:
  - Clears the stomach and the secondary stomach, 50u per cycle.
  - Output goes to a beaker loaded in it, or to a container or chem machine you've attached it to, or onto the floor.
  - It comes in the surgical duffel bag and the surgical supplies crate, and on Relic and Snowball it's on a medbay table.
- The health analyzer shows undigested plant matter.

**Chemistry and atmos**
- New reagents:
  - Saltpetre, made from K + N + 3 O.
  - Enduro Grow and Liquid Earthquake, from the biogenerator.
- New gases:
  - **BZ** forms from N₂O + plasma (each ≥10 mol, below 313 K). It causes hallucinations and poison.
  - **Healium** forms from BZ + frezon (frezon stands in for freon) between 25 and 300 K. It causes healing sleep. Breathing it or getting it in the bloodstream does the same.
- **Metalgen:**
  - Its recipe is random: 2–5 craftable medicines, a random temperature, and an artifexium catalyst. It makes 20u.
  - The recipe is saved on the server (`metalgen_recipe.json` in server data) and resets every 3 days.
  - Mercury plus 40u of iron, gold, silver, uranium or plasma imprints it.
  - Splashing imprinted metalgen transmutes objects: their material, what they recycle into, their color and their name.
  - The **Old Recipe** paper (5% chance in the chemistry locker) shows the current recipe.

**Maps.** Every station has a powered plant DNA manipulator and seed splicer in hydroponics, as every Monkestation station does.

**Lag**
- Seed and produce genomes aren't networked.
- Extractor storage is data, not entities.
- Strange seeds print one per 0.1 s.
- Seed batches share a tile.

## Monkestation quirks kept on purpose
- Weeds only come from reagents; weed rate and chance never grow weeds on their own.
- Honeydew can "mutate" into fresh honeydew seeds.
- Lime↔orange, poppy→lily and cannabis→rainbow have no stat window, so they're always viable.
- The "halves yield" training penalty actually raises yield by 50%.
- The DNA manipulator caps extracted yield at 10, even though most plants start at 30–60 yield.
- Inserting a chemical gene only works when the disk's rate is higher. Monkestation's code has this check inverted; the port uses the intended behavior.

## Effects outside botany
- **Chemistry/Medical:** botany can mass-produce chemicals from strange seeds, at up to 420u per fruit.
- **Engineering/Science:** potato batteries hold potency × 3.6 (×150 with Electrical Activity), up to 1e9. SS14's best cell holds 1800.
- **Service:** grown produce follows Monkestation amounts, except cooking reagents (floored as above).
- **Atmos:** any mix of N₂O and plasma can form BZ.
- **Cargo/Security:** transmuted objects sell and recycle as their new metal.

## Removed SS14 botany
- Swab cross-pollination; the swab is kept for the diagnoser board.
- Plant gas exchange.
- The sample-taker on wirecutters.
- The SS14 tray UI.
- Killer-tomato DNA imprinting.

## Testing done
- Server and client builds.
- YAML linter: no errors.
- Unit tests: all pass, including the new botany formula tests.
- Integration tests: run group by group (see the delivery notes). This machine has 7 GB of RAM, which isn't enough for two upstream tests (`SpawnAndDeleteAllEntitiesOnDifferentMaps`, `SpawnAndDirtyAllEntities`), so they ran out of memory here.
