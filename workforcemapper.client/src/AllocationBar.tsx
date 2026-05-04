import type { ResourceMapping } from './types';

interface Props {
  percent: number;
  mappings: ResourceMapping[];
}

function barColor(pct: number) {
  if (pct > 100) return 'bg-red-500';
  if (pct >= 80) return 'bg-amber-400';
  return 'bg-green-500';
}

export default function AllocationBar({ percent, mappings }: Props) {
  const tooltip = mappings
    .map((m) => `${m.activity_name}: ${m.percent}%`)
    .join(' | ');

  const clampedWidth = Math.min(percent, 100);

  return (
    <div className="mt-2" title={tooltip}>
      <div className="flex justify-between text-xs mb-1">
        <span>{percent}% allocated</span>
      </div>
      <div className="w-full bg-gray-700 rounded h-2 overflow-hidden">
        <div
          className={`h-2 rounded transition-all ${barColor(percent)}`}
          style={{ width: `${clampedWidth}%` }}
        />
      </div>
    </div>
  );
}
