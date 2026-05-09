import { useState } from 'react';
import type { ProductRisk, DirectAppNode } from './ProductsView';

function formatArr(usd: number) {
  return `$${(usd / 1_000_000).toFixed(1)}m`;
}

const BADGE: Record<string, string> = {
  high: 'bg-red-600 text-white',
  medium: 'bg-amber-500 text-white',
  low: 'bg-green-600 text-white',
};

function RiskBadge({ risk }: { risk: string }) {
  return (
    <span className={`px-2 py-0.5 rounded text-xs font-medium ${BADGE[risk] ?? ''}`}>
      {risk}
    </span>
  );
}

function AppNode({ app }: { app: DirectAppNode }) {
  const [open, setOpen] = useState(true);
  const hasDeps = app.dependencies.length > 0;

  return (
    <li>
      <div className="flex items-center gap-2 py-1">
        {hasDeps ? (
          <button
            onClick={() => setOpen(o => !o)}
            className="text-gray-500 hover:text-gray-300 text-xs w-4 text-center"
            aria-label={open ? 'Collapse' : 'Expand'}
          >
            {open ? '▾' : '▸'}
          </button>
        ) : (
          <span className="w-4" />
        )}
        <span className="text-gray-200 text-sm">{app.name}</span>
        <RiskBadge risk={app.risk} />
      </div>

      {hasDeps && open && (
        <ul className="ml-8 border-l border-gray-700 pl-3">
          {app.dependencies.map(dep => (
            <li key={dep.id} className="flex items-center gap-2 py-1">
              <span className="text-gray-300 text-sm">{dep.name}</span>
              <RiskBadge risk={dep.risk} />
            </li>
          ))}
        </ul>
      )}
    </li>
  );
}

interface Props {
  product: ProductRisk;
  onBack: () => void;
}

export default function ProductDetail({ product, onBack }: Props) {
  return (
    <div className="p-4 text-gray-200">
      <button onClick={onBack} className="text-blue-400 hover:underline mb-4 text-sm">
        &larr; Back to Products
      </button>

      <div className="flex items-center gap-3 mb-1">
        <h2 className="text-xl font-semibold">{product.name}</h2>
        <RiskBadge risk={product.risk} />
      </div>
      <p className="text-gray-400 text-sm mb-6">{formatArr(product.arr)} ARR</p>

      {product.direct_apps.length > 0 && (
        <div>
          <h3 className="text-sm font-medium text-gray-400 mb-2">Dependency Tree</h3>
          <ul className="space-y-0.5">
            {product.direct_apps.map(app => (
              <AppNode key={app.id} app={app} />
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}
