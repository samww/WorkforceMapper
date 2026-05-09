export type RawAlloc = { activity_id: number; percent: number };
export type ResourceBlock = { resource_id: string; allocations: RawAlloc[] };
export type FlatMapping = { resource_id: string; activity_id: number; percent: number; month: string };
export type Threshold = { app_id: number; name: string; support_activity_ids: number[]; min_support_fte: number };

/** Mulberry32 seeded PRNG — returns values in [0, 1). */
export function makePrng(seed: number): () => number {
  let s = seed;
  return () => {
    s = (s + 0x6D2B79F5) | 0;
    let t = Math.imul(s ^ (s >>> 15), 1 | s);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

/**
 * Clamps all entries to ≥ 1%, then uses a largest-remainder distribution to
 * assign integer percents that sum to exactly 100.
 */
export function normalise(block: ResourceBlock): ResourceBlock {
  const clamped = block.allocations.map(a => ({ ...a, percent: Math.max(1, a.percent) }));
  const total = clamped.reduce((s, a) => s + a.percent, 0);

  const exact = clamped.map(a => (a.percent / total) * 100);
  const floored = exact.map(v => Math.floor(v));
  const remainder = 100 - floored.reduce((s, v) => s + v, 0);

  // Sort indices by fractional part descending; ties preserve original order (stable)
  const order = exact
    .map((v, i) => ({ i, frac: v - Math.floor(v) }))
    .sort((a, b) => b.frac - a.frac || a.i - b.i)
    .map(x => x.i);

  const ints = [...floored];
  for (let k = 0; k < remainder; k++) ints[order[k]] += 1;

  return {
    ...block,
    allocations: clamped.map((a, i) => ({ ...a, percent: ints[i] })),
  };
}

/**
 * Adds a random integer delta drawn uniformly from [-8, +8] to each allocation,
 * then normalises so the resource sums to exactly 100% with all entries ≥ 1%.
 */
export function applyNoiseAndNormalise(block: ResourceBlock, rng: () => number): ResourceBlock {
  const noisy = block.allocations.map(a => {
    const delta = Math.round(rng() * 16 - 8);
    return { ...a, percent: a.percent + delta };
  });
  return normalise({ ...block, allocations: noisy });
}

/** Sum of percent/100 across mappings that match the given month and activity. */
export function computeCoverage(mappings: FlatMapping[], month: string, supportActivityId: number): number {
  return mappings
    .filter(m => m.month === month && m.activity_id === supportActivityId)
    .reduce((sum, m) => sum + m.percent / 100, 0);
}

/**
 * Guarantees at least one application is high-risk (coverage < min_support_fte)
 * in the given month. If one already exists, returns the same array unchanged.
 *
 * Otherwise selects the application with the smallest gap to its threshold
 * (closest to tipping over), then reduces the largest allocation to its support
 * activity by the minimum amount needed to push coverage below min_support_fte.
 * The freed percentage is given to the next-largest other activity of that
 * resource so the resource sum stays at exactly 100%.
 *
 * Single-allocation resources are skipped when selecting the target resource
 * (no other activity to absorb the freed %).
 */
export function ensureHighRisk(mappings: FlatMapping[], month: string, thresholds: Threshold[]): FlatMapping[] {
  const coverage = (actId: number) => computeCoverage(mappings, month, actId);

  const alreadyHighRisk = thresholds.some(t => coverage(t.support_activity_ids[0]) < t.min_support_fte);
  if (alreadyHighRisk) return mappings;

  // Find app with smallest positive gap from threshold
  const { t, gap } = thresholds
    .map(t => ({ t, gap: coverage(t.support_activity_ids[0]) - t.min_support_fte }))
    .sort((a, b) => a.gap - b.gap)[0];

  const supportActId = t.support_activity_ids[0];

  // Minimum integer-percent reduction to push coverage strictly below min
  const reductionPct = Math.ceil((gap + 0.001) * 100);

  // Find resource with largest allocation to this support activity that also has other activities
  const candidates = mappings.filter(m => {
    if (m.month !== month || m.activity_id !== supportActId) return false;
    const hasOthers = mappings.some(o => o.month === month && o.resource_id === m.resource_id && o.activity_id !== supportActId);
    return hasOthers;
  });

  if (candidates.length === 0) return mappings; // edge case: all support resources have only one activity

  const maxM = candidates.reduce((best, m) => (m.percent > best.percent ? m : best));
  const resourceId = maxM.resource_id;

  const newSupportPct = Math.max(1, maxM.percent - reductionPct);
  const freedPct = maxM.percent - newSupportPct;

  // Give freed % to the resource's largest non-support activity
  const otherMappings = mappings.filter(m => m.month === month && m.resource_id === resourceId && m.activity_id !== supportActId);
  const largestOther = otherMappings.reduce((best, m) => (m.percent > best.percent ? m : best));

  return mappings.map(m => {
    if (m.month !== month || m.resource_id !== resourceId) return m;
    if (m.activity_id === supportActId) return { ...m, percent: newSupportPct };
    if (m.activity_id === largestOther.activity_id) return { ...m, percent: m.percent + freedPct };
    return m;
  });
}
