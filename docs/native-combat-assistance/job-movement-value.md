# Job-aware movement value

The native route comparison values stationary time for Black Mage, Pictomancer,
Red Mage, healers, Arcanist/Thaumaturge/Conjurer and Blue Mage. Summoner and physical
jobs retain charge conservation unless the current captured sequence contains a
hardcast. This is a conservative job policy, not a future rotation or damage forecast.

Only the observed GCD countdown and recasts of actually delivered GCDs provide
free movement windows. Outside those windows, walking, mobility preparation,
travel and action locks cost stationary seconds. Sprint's overlapping movement
and animation lock are counted once; a short walk can beat its residual lock.
Free movement windows do not forecast later instant procs or repeated attacks.

The comparison retains current action delivery, required recipients, native goals,
healing needs and concrete combat/contact objectives first. When these tie, less
stationary time lost outranks saving a mobility charge, soft zone preferences and
cosmetic positioning. Mobile jobs keep their existing inventory-first behavior.
Safety, arena bounds, traversal deadlines, action ownership and saved mobility
opt-outs remain independent requirements.

The worker receives only a frozen job policy and GCD clock. Context equivalence
tracks the countdown and invalidates on job/policy changes. Resource pruning cannot
discard mobility when a complete caster route still loses stationary time; frontier
dominance includes accumulated cost and unequal remaining movement windows/locks.
Current diagnostics expose `MovementLossSeconds` for the selected route.

Regression coverage compares walking, targeted movement and Sprint, expensive
locks, free GCD windows, mobile jobs, selected casts, complete route validation,
and pruned versus unpruned search. Build/regression success is offline evidence;
representative live caster movement and uptime still require observation.
