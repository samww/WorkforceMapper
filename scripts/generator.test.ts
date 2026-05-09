import { describe, it, expect } from 'vitest';
import {
  normalise,
  applyNoiseAndNormalise,
  computeCoverage,
  ensureHighRisk,
  makePrng,
  type FlatMapping,
  type Threshold,
} from './generator.js';

// ── normalise ────────────────────────────────────────────────────────────────

describe('normalise', () => {
  it('output sums to exactly 100', () => {
    const block = {
      resource_id: 'PI00001',
      allocations: [
        { activity_id: 1, percent: 18 },
        { activity_id: 3, percent: 17 },
        { activity_id: 7, percent: 26 },
        { activity_id: 8, percent: 22 },
        { activity_id: 5, percent: 17 },
      ],
    };
    const { allocations } = normalise(block);
    const sum = allocations.reduce((s, a) => s + a.percent, 0);
    expect(sum).toBe(100);
  });

  it('all entries are at least 1%', () => {
    const block = {
      resource_id: 'PI00001',
      allocations: [
        { activity_id: 1, percent: 0.5 },
        { activity_id: 2, percent: 99.5 },
      ],
    };
    const { allocations } = normalise(block);
    allocations.forEach(a => expect(a.percent).toBeGreaterThanOrEqual(1));
  });

  it('each percent is a whole integer', () => {
    const block = {
      resource_id: 'R1',
      allocations: [
        { activity_id: 1, percent: 1 },
        { activity_id: 2, percent: 1 },
        { activity_id: 3, percent: 1 },
      ],
    };
    const { allocations } = normalise(block);
    allocations.forEach(a => expect(Number.isInteger(a.percent)).toBe(true));
  });

  it('largest-remainder distributes residual to first entry by fractional rank', () => {
    // Three equal allocations: each floors to 33, one remainder unit distributed to first
    const block = {
      resource_id: 'R1',
      allocations: [
        { activity_id: 1, percent: 1 },
        { activity_id: 2, percent: 1 },
        { activity_id: 3, percent: 1 },
      ],
    };
    const { allocations } = normalise(block);
    const sum = allocations.reduce((s, a) => s + a.percent, 0);
    expect(sum).toBe(100);
    expect(Math.max(...allocations.map(a => a.percent))).toBe(34);
  });
});

// ── applyNoiseAndNormalise ───────────────────────────────────────────────────

describe('applyNoiseAndNormalise', () => {
  it('output sums to exactly 100 and all entries are at least 1%', () => {
    const block = {
      resource_id: 'PI00001',
      allocations: [
        { activity_id: 1, percent: 18 },
        { activity_id: 3, percent: 17 },
        { activity_id: 7, percent: 26 },
        { activity_id: 8, percent: 22 },
        { activity_id: 5, percent: 17 },
      ],
    };
    const rng = makePrng(42);
    const { allocations } = applyNoiseAndNormalise(block, rng);
    const sum = allocations.reduce((s, a) => s + a.percent, 0);
    expect(sum).toBe(100);
    allocations.forEach(a => expect(a.percent).toBeGreaterThanOrEqual(1));
  });

  it('each percent is a whole integer', () => {
    const block = {
      resource_id: 'PI00001',
      allocations: [
        { activity_id: 1, percent: 18 },
        { activity_id: 3, percent: 17 },
        { activity_id: 7, percent: 26 },
        { activity_id: 8, percent: 22 },
        { activity_id: 5, percent: 17 },
      ],
    };
    const rng = makePrng(42);
    const { allocations } = applyNoiseAndNormalise(block, rng);
    allocations.forEach(a => expect(Number.isInteger(a.percent)).toBe(true));
  });
});

// ── computeCoverage ──────────────────────────────────────────────────────────

describe('computeCoverage', () => {
  it('sums percent/100 for the given support activity in the given month only', () => {
    const mappings: FlatMapping[] = [
      { resource_id: 'R1', activity_id: 5, percent: 40, month: '2026-01' },
      { resource_id: 'R2', activity_id: 5, percent: 60, month: '2026-01' },
      { resource_id: 'R3', activity_id: 5, percent: 50, month: '2026-02' }, // different month — excluded
      { resource_id: 'R4', activity_id: 1, percent: 100, month: '2026-01' }, // different activity — excluded
    ];
    const coverage = computeCoverage(mappings, '2026-01', 5);
    expect(coverage).toBeCloseTo(1.0, 5);
  });
});

// ── ensureHighRisk ───────────────────────────────────────────────────────────

const threshold: Threshold = {
  app_id: 4,
  name: 'AuthService',
  support_activity_ids: [5],
  min_support_fte: 4.0,
};

/** 5 resources, each with 90% on activity 5 + 10% on activity 99 → coverage 4.5 FTE */
function makeCoverage45(): FlatMapping[] {
  return [1, 2, 3, 4, 5].flatMap(n => [
    { resource_id: `R${n}`, activity_id: 5,  percent: 90, month: '2026-01' },
    { resource_id: `R${n}`, activity_id: 99, percent: 10, month: '2026-01' },
  ]);
}

/** 3 resources, each with 60% on activity 5 + 40% on activity 99 → coverage 1.8 FTE (already high-risk) */
function makeAlreadyHighRisk(): FlatMapping[] {
  return [1, 2, 3].flatMap(n => [
    { resource_id: `R${n}`, activity_id: 5,  percent: 60, month: '2026-01' },
    { resource_id: `R${n}`, activity_id: 99, percent: 40, month: '2026-01' },
  ]);
}

describe('ensureHighRisk', () => {
  it('reduces coverage below min_support_fte when no app is high-risk', () => {
    const mappings = makeCoverage45();
    const result = ensureHighRisk(mappings, '2026-01', [threshold]);
    const coverage = computeCoverage(result, '2026-01', 5);
    expect(coverage).toBeLessThan(4.0);
  });

  it('is a no-op when a high-risk app is already present', () => {
    const mappings = makeAlreadyHighRisk();
    const result = ensureHighRisk(mappings, '2026-01', [threshold]);
    expect(result).toEqual(mappings);
  });

  it('all percents remain integers after adjustments', () => {
    const mappings = makeCoverage45();
    const result = ensureHighRisk(mappings, '2026-01', [threshold]);
    result.forEach(m => expect(Number.isInteger(m.percent)).toBe(true));
  });
});
