## Monkestation botany port

# Stats and tray attributes
plant-stat-potency = potency
plant-stat-yield = yield
plant-stat-production = production speed
plant-stat-endurance = endurance
plant-stat-lifespan = lifespan
plant-stat-weedrate = weed growth rate
plant-stat-weedchance = weed vulnerability
plant-stat-maturation = maturation speed

plant-tray-attribute-toxicity = toxicity
plant-tray-attribute-pests = pests
plant-tray-attribute-weeds = weeds
plant-tray-attribute-selfsustain = self-sustenance

# Gene names
plant-experimental-name = experimental {$name}
plant-gene-chemical = {$prefix}{$reagent} production {$rate}%
plant-gene-fragile = Fragile
plant-gene-immutable = Immutable
plant-gene-essential = Essential
plant-gene-unknown = unknown gene

# Tray
tray-component-harvestable = It's ready to [color=yellow]harvest[/color].
tray-component-self-sustaining = It's [color=lightgreen]self-sustaining[/color].
plant-holder-component-plant-success-message = You plant the {$seedName}.
plant-holder-component-already-seeded-message = { CAPITALIZE(THE($name)) } already has something planted in it!
plant-cannot-plant-sample = This is only a sample. It can't be planted.
plant-cultivator-weeds-removed = You uproot the weeds from { THE($tray) }.
plant-spade-removed = You dig the plant out of { THE($tray) }.
plant-remove-dead = You remove the dead plant from { THE($tray) }.
plant-harvest-too-crowded = There's too much here already to harvest any more.

# Grafting
plant-graft-name = {$plant} graft
plant-graft-cut = You take a graft from the {$plant}.
plant-graft-already-taken = This plant has already been grafted.
plant-graft-applied = You graft {$trait} onto the {$plant}.
plant-graft-applied-no-trait = You graft the cutting onto the {$plant}, but it can't take {$trait}.
plant-graft-examine = It carries [color=yellow]{$trait}[/color], cut from {$plant}.

# Data disks
plant-disk-name = {$gene} (plant data disk)
plant-disk-examine-gene = It holds [color=yellow]{$gene}[/color].
plant-disk-examine-empty = It's blank.
plant-disk-examine-potency-note = Potency genes are capped by the manipulator they were extracted with.
plant-disk-examine-protected = The write-protect tab is [color=red]on[/color].
plant-disk-examine-unprotected = The write-protect tab is [color=lightgreen]off[/color].
plant-disk-protect-on = You flip the write-protect tab on.
plant-disk-protect-off = You flip the write-protect tab off.

# DNA manipulator
plant-dna-window-title = Plant DNA Manipulator
plant-dna-sample = Seed:
plant-dna-disk = Disk:
plant-dna-eject = Eject
plant-dna-no-sample = No seed inserted
plant-dna-no-disk = No disk inserted
plant-dna-empty-disk = blank disk
plant-dna-disk-readonly = {$name} (write-protected)
plant-dna-insert-sample = Insert a seed to see its genes.
plant-dna-core-genes = Core genes
plant-dna-chemical-genes = Chemical genes
plant-dna-trait-genes = Trait genes
plant-dna-no-chemical-genes = No chemical genes.
plant-dna-no-trait-genes = No trait genes.
plant-dna-extract = Extract
plant-dna-extract-confirm = Destroy seed?
plant-dna-extract-tooltip = Copies this gene to the disk. The seed is destroyed.
plant-dna-extract-tooltip-degrade = Copies this gene to the disk. The seed is destroyed. {$warning}
plant-dna-extract-degrade = This machine can only extract it as {$value}.
plant-dna-replace = Replace
plant-dna-remove = Remove
plant-dna-confirm = Confirm?
plant-dna-insert = Insert {$gene}
plant-dna-extracted = You extract {$gene} onto the disk.
plant-dna-inserted = You insert {$gene}.
plant-dna-replaced = You replace the gene with {$gene}.
plant-dna-removed = You remove {$gene}.
plant-dna-slot-seed = Seed
plant-dna-slot-disk = Disk

# Seed splicer
plant-splicer-window-title = Seed Splicer
plant-splicer-first-seed = First seed:
plant-splicer-second-seed = Second seed:
plant-splicer-beaker = Beaker:
plant-splicer-splice = Splice
plant-splicer-infuse = Infuse
plant-splicer-empty-slot = Empty
plant-splicer-no-beaker = No beaker inserted.
plant-splicer-stats = Potency {$potency}, yield {$yield}, endurance {$endurance}, lifespan {$lifespan}, maturation {$maturation}, production {$production}
plant-splicer-damage = Infusion damage: {$current} + {$added} = {$total} / 100
plant-splicer-will-infuse = (will become a gene)
plant-splicer-beaker-empty = The beaker is empty.
plant-splicer-cannot-infuse = {$reagent} can't be infused into plants!
plant-splicer-seed-destroyed = { CAPITALIZE(THE($seed)) } has become too damaged from infusion and disintegrated!
plant-splicer-infused = You infuse { THE($seed) }. {$count ->
    [0] No new genes took.
    [one] One gene took.
   *[other] {$count} genes took.
}
plant-splicer-spliced = You splice the seeds into {$name}.
plant-splicer-slot-seed-one = First seed
plant-splicer-slot-seed-two = Second seed
plant-splicer-slot-beaker = Beaker

# Plant analyzer
plant-analyzer-window-title = Plant Analyzer
plant-analyzer-no-plant = Nothing is planted.
plant-analyzer-no-genes = It has no genes to read.
plant-analyzer-stats-header = [bold]Stats[/bold]
plant-analyzer-stat-line = {$stat}: {$value}
plant-analyzer-harvest-age = Harvest age: {$age} cycles
plant-analyzer-traits-header = [bold]Traits[/bold]
plant-analyzer-chems-header = [bold]Chemicals[/bold]
plant-analyzer-no-chems = None
plant-analyzer-list-line = - {$text}
plant-analyzer-volume = Reagent volume: {$volume}u
plant-analyzer-spliced = This is a spliced plant.
plant-analyzer-training-header = [bold]Trait training[/bold]
plant-analyzer-training-line = {$trait}: {$progress}%
plant-analyzer-graft-trait = Graftable trait: {$trait}
plant-analyzer-health = Health: {$health} / {$max}
plant-analyzer-age = Age: {$age} cycles
plant-analyzer-growth = Growth: {$growth}%
plant-analyzer-water = Water: {$water}%
plant-analyzer-toxicity = Toxicity: {$toxicity}
plant-analyzer-pests = Pests: {$pests} / {$max}
plant-analyzer-weeds = Weeds: {$weeds} / {$max}
plant-analyzer-self-sustain = Self-sustenance: {$percent}%
plant-analyzer-medium-header = [bold]Growing medium[/bold]
plant-analyzer-medium-contains = It contains:
plant-analyzer-contents-header = [bold]Contents[/bold] ({$volume} / {$max}u)
plant-analyzer-no-reagents = No reagents.
plant-analyzer-reagent-line = - {$amount}u {$reagent}

# Trait effects
plant-squashed = The {$plant} is squashed!
plant-teleport-slip = You slip through spacetime!
plant-laughter = The {$plant} lets out a burst of laughter!
plant-prickles-pricked = You're pricked by the {$plant}!
plant-prickles-blocked = The prickles fail to get through { THE($target) }'s armor.
plant-battery-need-cable = You need {$amount} lengths of cable to make a battery out of the {$plant}.
plant-battery-made = You wire the {$plant} into a battery.
plant-battery-name = {$plant} battery

# Seed extractor
seed-extractor-component-bag-message = You extract seeds from {$count ->
    [one] one plant
   *[other] {$count} plants
}.
seed-extractor-stored = You put { THE($seed) } into { THE($machine) }.
seed-extractor-cannot-store = You can't seem to put { THE($seed) } into the machine.
seed-extractor-full = { CAPITALIZE(THE($machine)) } is full.
seed-extractor-bag-stored = You put {$count} {$count ->
    [one] seed
   *[other] seeds
} into { THE($machine) }.
seed-extractor-bag-stored-full = You put {$count} {$count ->
    [one] seed
   *[other] seeds
} into { THE($machine) } before it filled up.
seed-extractor-take = You take { THE($seed) } out of the slot.
seed-extractor-take-floor = { CAPITALIZE(THE($seed)) } falls onto the floor.
seed-extractor-window-title = Seed Extractor
seed-extractor-search = Search seeds, traits or chemicals...
seed-extractor-capacity = {$stored} / {$capacity} seeds
seed-extractor-empty = No seeds stored. Put seeds in by hand, or use a bag with seeds in it.
seed-extractor-pile-name = {$name} (x{$amount})
seed-extractor-pile-stats = Potency {$potency} | Yield {$yield} | Production {$production} | Endurance {$endurance} | Lifespan {$lifespan} | Maturation {$maturation}
seed-extractor-pile-traits = Traits: {$traits}
seed-extractor-pile-chemicals = Chemicals: {$chemicals}
seed-extractor-pile-mutations = Mutates into: {$mutations}
seed-extractor-chemical = {$reagent} {$rate}%
seed-extractor-take-button = Take
seed-extractor-scrap-button = Scrap
seed-extractor-scrap-confirm = Destroy all?

# Biogenerator
material-extractor-comp-bag = You empty {$count ->
    [one] one plant
   *[other] {$count} plants
} into the machine.

# Reagent effects on trays (guidebook)
entity-effect-guidebook-plant-per-unit = {$amount} per unit
entity-effect-guidebook-plant-flat-range = {$min} to {$max}
entity-effect-guidebook-plant-stat = { $chance ->
    [1] { $positive ->
        [true] Increases
       *[false] Decreases
    }
   *[other] { $positive ->
        [true] increase
       *[false] decrease
    }
} a plant's {$stat} by {$amount}
entity-effect-guidebook-plant-health = { $chance ->
    [1] { $positive ->
        [true] Heals
       *[false] Damages
    }
   *[other] { $positive ->
        [true] heal
       *[false] damage
    }
} a plant by {$amount}
entity-effect-guidebook-plant-tray = { $chance ->
    [1] { $positive ->
        [true] Raises
       *[false] Lowers
    }
   *[other] { $positive ->
        [true] raise
       *[false] lower
    }
} a tray's {$attribute} by {$amount}
entity-effect-guidebook-plant-train-trait = { $chance ->
    [1] Trains
   *[other] train
} a plant towards {$trait}
entity-effect-guidebook-plant-speed-up = { $chance ->
    [1] Speeds
   *[other] speed
} up a plant's growth by {$time} seconds

# Stomach pump
stomach-pump-start = You start pumping { THE($target) }'s stomach.
stomach-pump-progress = Still {$remaining}u to go...
stomach-pump-done = { CAPITALIZE(THE($target)) }'s stomach is empty.
stomach-pump-nothing = { CAPITALIZE(THE($target)) }'s stomach is already empty.
stomach-pump-attached = You run the pump's hose to { THE($target) }.
stomach-pump-detached = You disconnect the pump's hose from { THE($target) }.
stomach-pump-hose-detached = The pump's hose comes loose!
stomach-pump-slot-beaker = Beaker

# Portable seed extractor
portable-seed-extractor-verb = Make seeds
portable-seed-extractor-done = You turn {$count ->
    [one] one plant
   *[other] {$count} plants
} into seeds.
portable-seed-extractor-nothing = There's nothing in here to make seeds from.
