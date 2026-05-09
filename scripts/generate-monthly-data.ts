import { readFileSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import {
  makePrng,
  applyNoiseAndNormalise,
  ensureHighRisk,
  type FlatMapping,
  type ResourceBlock,
  type Threshold,
} from './generator.js';

const __dirname = dirname(fileURLToPath(import.meta.url));
const root = join(__dirname, '..');

const baseMappings: FlatMapping[] = JSON.parse(readFileSync(join(root, 'data', 'mappings.json'), 'utf8'));
const thresholds: Threshold[] = JSON.parse(readFileSync(join(__dirname, 'thresholds.json'), 'utf8'));

const mayMappings = baseMappings.filter(m => m.month === '2026-05');

const NON_MAY_MONTHS = [
  '2026-01', '2026-02', '2026-03', '2026-04',
  '2026-06', '2026-07', '2026-08', '2026-09', '2026-10', '2026-11', '2026-12',
];

function generateMonth(baseMay: FlatMapping[], targetMonth: string, seed: number): FlatMapping[] {
  const rng = makePrng(seed);

  // Group May mappings by resource
  const byResource = new Map<string, FlatMapping[]>();
  for (const m of baseMay) {
    const arr = byResource.get(m.resource_id) ?? [];
    arr.push(m);
    byResource.set(m.resource_id, arr);
  }

  const result: FlatMapping[] = [];
  for (const [resourceId, allocs] of byResource) {
    const block: ResourceBlock = {
      resource_id: resourceId,
      allocations: allocs.map(m => ({ activity_id: m.activity_id, percent: m.percent })),
    };
    const noised = applyNoiseAndNormalise(block, rng);
    for (const a of noised.allocations) {
      result.push({ resource_id: resourceId, activity_id: a.activity_id, percent: a.percent, month: targetMonth });
    }
  }

  return ensureHighRisk(result, targetMonth, thresholds);
}

const allMappings: FlatMapping[] = [...mayMappings];

for (const month of NON_MAY_MONTHS) {
  const monthNum = parseInt(month.slice(5), 10);
  const seed = monthNum * 1000 + 17;
  const generated = generateMonth(mayMappings, month, seed);
  allMappings.push(...generated);

  // Report which app is high-risk for this month
  for (const t of thresholds) {
    const actId = t.support_activity_ids[0];
    const cov = generated.filter(m => m.activity_id === actId).reduce((s, m) => s + m.percent / 100, 0);
    if (cov < t.min_support_fte) {
      console.log(`${month}: ${t.name} HIGH (coverage ${cov.toFixed(2)} FTE, min ${t.min_support_fte})`);
    }
  }
}

writeFileSync(join(root, 'data', 'mappings.json'), JSON.stringify(allMappings, null, 2) + '\n');
console.log(`\nWrote ${allMappings.length} mappings (${mayMappings.length} May + ${allMappings.length - mayMappings.length} generated)`);
