import type { ProductRisk } from './ProductsView';
import MermaidDiagram from './MermaidDiagram';
import { buildProductMermaid } from './diagramBuilders';

function formatArr(usd: number) {
  return `$${(usd / 1_000_000).toFixed(1)}m`;
}

const BADGE: Record<string, string> = {
  high: 'bg-red-600 text-white',
  medium: 'bg-amber-500 text-white',
  low: 'bg-green-600 text-white',
};

interface Props {
  product: ProductRisk;
  onBack: () => void;
  backLabel?: string;
  onNavigateToApp?: (appId: number) => void;
}

export default function ProductDetail({ product, onBack, backLabel = 'Back to Products', onNavigateToApp }: Props) {
  return (
    <div className="p-4 text-gray-200">
      <button onClick={onBack} className="text-blue-400 hover:underline mb-4 text-sm">
        &larr; {backLabel}
      </button>

      <div className="flex items-center gap-3 mb-1">
        <h2 className="text-xl font-semibold">{product.name}</h2>
        <span className={`px-2 py-0.5 rounded text-xs font-medium ${BADGE[product.risk] ?? ''}`}>
          {product.risk}
        </span>
      </div>
      <p className="text-gray-400 text-sm mb-6">{formatArr(product.arr)} ARR</p>

      {product.direct_apps.length > 0 && (
        <div className="mb-8">
          <h3 className="text-sm font-medium text-gray-400 mb-2">Dependency Tree</h3>
          <MermaidDiagram
            definition={buildProductMermaid(product.direct_apps, product.name)}
            onNodeClick={onNavigateToApp ? (nodeId) => {
              const m = nodeId.match(/^app(\d+)$/);
              if (m) onNavigateToApp(parseInt(m[1]));
            } : undefined}
          />
        </div>
      )}

      {product.contributing_apps.length > 0 && (
        <div>
          <h3 className="text-sm font-medium text-gray-400 mb-2">Contributing Applications</h3>
          <table className="w-full text-sm text-gray-200">
            <thead>
              <tr className="text-left text-gray-400 border-b border-gray-700">
                <th className="pb-2 pr-4">Application</th>
                <th className="pb-2 pr-4">Division</th>
                <th className="pb-2">Risk</th>
              </tr>
            </thead>
            <tbody>
              {product.contributing_apps.map(a => (
                <tr key={a.id} className="border-b border-gray-800 hover:bg-gray-800">
                  <td className="py-2 pr-4">
                    {onNavigateToApp ? (
                      <button
                        onClick={() => onNavigateToApp(a.id)}
                        className="text-blue-400 hover:underline text-left"
                      >
                        {a.name}
                      </button>
                    ) : a.name}
                  </td>
                  <td className="py-2 pr-4 text-gray-400">{a.division}</td>
                  <td className="py-2">
                    <span className={`px-2 py-0.5 rounded text-xs font-medium ${BADGE[a.risk] ?? ''}`}>
                      {a.risk}
                    </span>
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
