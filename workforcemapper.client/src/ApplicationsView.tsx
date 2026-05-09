import { useEffect, useState } from 'react';

export interface ProductRef {
  id: number;
  name: string;
  arr_usd: number;
  risk?: 'high' | 'medium' | 'low';
}

export interface ApplicationRisk {
  id: number;
  name: string;
  division: string;
  min_support_fte: number;
  recommended_support_fte: number;
  current_coverage_fte: number;
  risk: 'high' | 'medium' | 'low';
  dependent_products: ProductRef[];
}

interface Props {
  month: string;
  onSelectApp: (app: ApplicationRisk) => void;
}

const BADGE: Record<string, string> = {
  high: 'bg-red-600 text-white',
  medium: 'bg-amber-500 text-white',
  low: 'bg-green-600 text-white',
};

function CoverageBar({ current, min, recommended }: { current: number; min: number; recommended: number }) {
  const barMax = Math.max(recommended * 1.2, current);
  const coveragePct = Math.min((current / barMax) * 100, 100);
  const minPct = (min / barMax) * 100;
  const recPct = (recommended / barMax) * 100;
  return (
    <div className="w-40">
      <div className="relative h-2.5 bg-gray-700 rounded">
        <div className="h-full bg-blue-500 rounded" style={{ width: `${coveragePct}%` }} />
        <div
          className="absolute top-0 h-full w-0.5 bg-amber-400"
          style={{ left: `${minPct}%` }}
          title={`Min: ${min.toFixed(1)} FTE`}
        />
        <div
          className="absolute top-0 h-full w-0.5 bg-green-400"
          style={{ left: `${recPct}%` }}
          title={`Recommended: ${recommended.toFixed(1)} FTE`}
        />
      </div>
      <div className="flex justify-between text-xs text-gray-500 mt-0.5">
        <span>{current.toFixed(1)}</span>
        <span>{min.toFixed(1)} / {recommended.toFixed(1)}</span>
      </div>
    </div>
  );
}

export default function ApplicationsView({ month, onSelectApp }: Props) {
  const [apps, setApps] = useState<ApplicationRisk[]>([]);

  useEffect(() => {
    fetch(`/api/applications?month=${month}`)
      .then(r => r.json())
      .then((data: ApplicationRisk[]) => {
        const riskOrder = { high: 0, medium: 1, low: 2 };
        data.sort((a, b) => {
          const rd = riskOrder[a.risk] - riskOrder[b.risk];
          if (rd !== 0) return rd;
          const gapA = a.min_support_fte - a.current_coverage_fte;
          const gapB = b.min_support_fte - b.current_coverage_fte;
          return gapB - gapA;
        });
        setApps(data);
      });
  }, [month]);

  return (
    <div className="p-4">
      <h2 className="text-xl font-semibold text-gray-100 mb-4">Applications</h2>
      <table className="w-full text-sm text-gray-200">
        <thead>
          <tr className="text-left text-gray-400 border-b border-gray-700">
            <th className="pb-2 pr-4">Name</th>
            <th className="pb-2 pr-4">Division</th>
            <th className="pb-2 pr-4">Risk</th>
            <th className="pb-2">Coverage</th>
          </tr>
        </thead>
        <tbody>
          {apps.map(app => (
            <tr
              key={app.id}
              className="border-b border-gray-800 hover:bg-gray-800 cursor-pointer"
              onClick={() => onSelectApp(app)}
            >
              <td className="py-2 pr-4">{app.name}</td>
              <td className="py-2 pr-4 text-gray-400">{app.division}</td>
              <td className="py-2 pr-4">
                <span className={`px-2 py-0.5 rounded text-xs font-medium ${BADGE[app.risk]}`}>
                  {app.risk}
                </span>
              </td>
              <td className="py-2">
                <CoverageBar current={app.current_coverage_fte} min={app.min_support_fte} recommended={app.recommended_support_fte} />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
