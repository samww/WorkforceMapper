import type { ApplicationRisk } from './ApplicationsView';

const BADGE: Record<string, string> = {
  high: 'bg-red-600 text-white',
  medium: 'bg-amber-500 text-white',
  low: 'bg-green-600 text-white',
};

function formatArr(usd: number) {
  return `$${(usd / 1_000_000).toFixed(1)}m`;
}

interface Props {
  app: ApplicationRisk;
  onBack: () => void;
}

export default function ApplicationDetail({ app, onBack }: Props) {
  const barMax = Math.max(app.recommended_support_fte * 1.2, app.current_coverage_fte);
  const coveragePct = (app.current_coverage_fte / barMax) * 100;
  const minPct = (app.min_support_fte / barMax) * 100;
  const recPct = (app.recommended_support_fte / barMax) * 100;

  return (
    <div className="p-4 text-gray-200">
      <button onClick={onBack} className="text-blue-400 hover:underline mb-4 text-sm">
        &larr; Back to Applications
      </button>
      <h2 className="text-xl font-semibold mb-1">{app.name}</h2>
      <p className="text-gray-400 text-sm mb-6">{app.division}</p>

      <h3 className="text-sm font-medium text-gray-400 mb-2">Support Coverage</h3>
      <div className="relative h-5 bg-gray-700 rounded mb-1">
        <div
          className="h-full bg-blue-500 rounded"
          style={{ width: `${Math.min(coveragePct, 100)}%` }}
        />
        <div
          className="absolute top-0 h-full w-0.5 bg-amber-400"
          style={{ left: `${minPct}%` }}
          title={`Min: ${app.min_support_fte.toFixed(1)} FTE`}
        />
        <div
          className="absolute top-0 h-full w-0.5 bg-green-400"
          style={{ left: `${recPct}%` }}
          title={`Recommended: ${app.recommended_support_fte.toFixed(1)} FTE`}
        />
      </div>
      <div className="flex justify-between text-xs text-gray-400 mt-1">
        <span>{app.current_coverage_fte.toFixed(1)} FTE current</span>
        <span>Min {app.min_support_fte.toFixed(1)} &nbsp; Rec {app.recommended_support_fte.toFixed(1)}</span>
      </div>

      {app.dependent_products.length > 0 && (
        <div className="mt-8">
          <h3 className="text-sm font-medium text-gray-400 mb-2">Dependent Products</h3>
          <table className="w-full text-sm text-gray-200">
            <thead>
              <tr className="text-left text-gray-400 border-b border-gray-700">
                <th className="pb-2 pr-4">Product</th>
                <th className="pb-2 pr-4">ARR</th>
                <th className="pb-2">Risk</th>
              </tr>
            </thead>
            <tbody>
              {app.dependent_products.map(p => (
                <tr key={p.id} className="border-b border-gray-800">
                  <td className="py-2 pr-4">{p.name}</td>
                  <td className="py-2 text-gray-400 pr-4">{formatArr(p.arr_usd)}</td>
                  <td className="py-2">
                    {p.risk && (
                      <span className={`px-2 py-0.5 rounded text-xs font-medium ${BADGE[p.risk]}`}>
                        {p.risk}
                      </span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
