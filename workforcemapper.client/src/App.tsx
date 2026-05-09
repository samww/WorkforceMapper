import { useState } from 'react';
import MappingView from './MappingView';
import ChatPanel from './ChatPanel';
import ApplicationsView from './ApplicationsView';
import type { ApplicationRisk } from './ApplicationsView';
import ApplicationDetail from './ApplicationDetail';
import ProductsView from './ProductsView';
import type { ProductRisk } from './ProductsView';
import ProductDetail from './ProductDetail';

type Tab = 'mappings' | 'applications' | 'products';

const DEFAULT_MONTH = '2026-05';
const DIVISIONS = ['All', 'Platform Engineering', 'Analytics', 'Data Collection', 'Client Services'];
const MONTH_LABELS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
const MONTH_OPTIONS = Array.from({ length: 12 }, (_, i) => ({
  value: `2026-${String(i + 1).padStart(2, '0')}`,
  label: `${MONTH_LABELS[i]} 2026`,
}));

export default function App() {
  const [chatOpen, setChatOpen] = useState(false);
  const [tab, setTab] = useState<Tab>('mappings');
  const [selectedApp, setSelectedApp] = useState<ApplicationRisk | null>(null);
  const [selectedProduct, setSelectedProduct] = useState<ProductRisk | null>(null);
  const [month, setMonth] = useState(DEFAULT_MONTH);
  const [division, setDivision] = useState('All');

  return (
    <div className="flex h-screen overflow-hidden">
      <div className="flex-1 overflow-auto min-w-0 flex flex-col">
        <nav className="flex gap-1 px-4 pt-4 pb-0 border-b border-gray-700">
          {(['mappings', 'applications', 'products'] as Tab[]).map(t => (
            <button
              key={t}
              onClick={() => { setTab(t); setSelectedApp(null); setSelectedProduct(null); }}
              className={`px-4 py-2 text-sm font-medium capitalize rounded-t transition-colors
                ${tab === t ? 'bg-gray-800 text-white' : 'text-gray-400 hover:text-gray-200'}`}
            >
              {t}
            </button>
          ))}
        </nav>

        <div className="flex flex-wrap gap-4 px-4 py-2 border-b border-gray-800 items-center bg-gray-900">
          <div>
            <label htmlFor="app-month-select" className="text-sm text-gray-400 mr-2">Month</label>
            <select
              id="app-month-select"
              aria-label="Month"
              value={month}
              onChange={e => setMonth(e.target.value)}
              className="bg-gray-800 border border-gray-600 rounded px-2 py-1 text-sm text-gray-100"
            >
              {MONTH_OPTIONS.map(o => (
                <option key={o.value} value={o.value}>{o.label}</option>
              ))}
            </select>
          </div>
          <div>
            <label htmlFor="app-division-select" className="text-sm text-gray-400 mr-2">Division</label>
            <select
              id="app-division-select"
              aria-label="Division"
              value={division}
              onChange={e => setDivision(e.target.value)}
              className="bg-gray-800 border border-gray-600 rounded px-2 py-1 text-sm text-gray-100"
            >
              {DIVISIONS.map(d => (
                <option key={d} value={d}>{d}</option>
              ))}
            </select>
          </div>
        </div>

        <div className="flex-1 overflow-auto">
          {tab === 'mappings' && (
            <MappingView month={month} division={division} onOpenChat={chatOpen ? undefined : () => setChatOpen(true)} />
          )}
          {tab === 'applications' && !selectedApp && (
            <ApplicationsView month={month} division={division} onSelectApp={setSelectedApp} />
          )}
          {tab === 'applications' && selectedApp && (
            <ApplicationDetail app={selectedApp} onBack={() => setSelectedApp(null)} />
          )}
          {tab === 'products' && !selectedProduct && (
            <ProductsView month={month} division={division} onSelectProduct={setSelectedProduct} />
          )}
          {tab === 'products' && selectedProduct && (
            <ProductDetail product={selectedProduct} onBack={() => setSelectedProduct(null)} />
          )}
        </div>
      </div>

      <div
        className={`flex-shrink-0 flex flex-col bg-gray-900 border-l border-gray-700 transition-all duration-300 overflow-hidden
          ${chatOpen ? 'w-[420px]' : 'w-0'}`}
      >
        <ChatPanel onClose={() => setChatOpen(false)} />
      </div>
    </div>
  );
}
