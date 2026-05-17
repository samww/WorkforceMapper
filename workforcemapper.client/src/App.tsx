import { useEffect, useState } from 'react';
import MappingView from './MappingView';
import ChatPanel from './ChatPanel';
import ApplicationsView from './ApplicationsView';
import type { ApplicationRisk } from './ApplicationsView';
import ApplicationDetail from './ApplicationDetail';
import ProductsView from './ProductsView';
import type { ProductRisk } from './ProductsView';
import ProductDetail from './ProductDetail';
import ProjectsView from './ProjectsView';

type Tab = 'mappings' | 'applications' | 'products' | 'projects';

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
  const [mobileNavOpen, setMobileNavOpen] = useState(false);
  const [selectedApp, setSelectedApp] = useState<ApplicationRisk | null>(null);
  const [selectedProduct, setSelectedProduct] = useState<ProductRisk | null>(null);
  const [productBackApp, setProductBackApp] = useState<ApplicationRisk | null>(null);
  const [appBackProduct, setAppBackProduct] = useState<ProductRisk | null>(null);
  const [month, setMonth] = useState(DEFAULT_MONTH);
  const [division, setDivision] = useState('All');

  const tabs: Tab[] = ['mappings', 'applications', 'products', 'projects'];

  function switchTopLevelSection(nextTab: Tab) {
    setTab(nextTab);
    setSelectedApp(null);
    setSelectedProduct(null);
    setProductBackApp(null);
    setAppBackProduct(null);
  }

  function switchFromMobileDrawer(nextTab: Tab) {
    switchTopLevelSection(nextTab);
    setMobileNavOpen(false);
  }

  useEffect(() => {
    if (!mobileNavOpen) return;

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        setMobileNavOpen(false);
      }
    }

    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [mobileNavOpen]);

  return (
    <div className="flex h-screen overflow-hidden">
      {mobileNavOpen && (
        <div className="fixed inset-0 z-40 sm:hidden">
          <button
            type="button"
            aria-label="Close navigation backdrop"
            onClick={() => setMobileNavOpen(false)}
            className="absolute inset-0 h-full w-full bg-black/60"
          />
          <div
            role="dialog"
            aria-modal="true"
            aria-label="Section navigation"
            className="relative flex h-full w-[min(20rem,85vw)] flex-col border-r border-gray-700 bg-gray-900 p-4 shadow-xl"
          >
            <div className="mb-4 flex items-center justify-between">
              <h2 className="text-sm font-semibold text-gray-200">Navigation</h2>
              <button
                type="button"
                aria-label="Close navigation"
                onClick={() => setMobileNavOpen(false)}
                className="min-h-11 rounded-lg px-3 py-2 text-sm text-gray-300 hover:bg-gray-800 hover:text-gray-100"
              >
                Close
              </button>
            </div>
            <nav aria-label="Mobile sections" className="flex flex-col gap-1">
              {tabs.map(t => (
                <button
                  key={t}
                  type="button"
                  onClick={() => switchFromMobileDrawer(t)}
                  className={`min-h-11 rounded-lg px-3 py-2 text-left text-sm font-medium capitalize transition-colors
                    ${tab === t ? 'bg-gray-800 text-white' : 'text-gray-300 hover:bg-gray-800 hover:text-gray-100'}`}
                >
                  {t}
                </button>
              ))}
            </nav>
          </div>
        </div>
      )}
      <div className="flex-1 overflow-auto min-w-0 flex flex-col">
        <header className="flex items-center gap-2 border-b border-gray-700 px-4 py-2">
          <button
            type="button"
            aria-label="Open navigation"
            onClick={() => setMobileNavOpen(true)}
            className="flex min-w-9 flex-col items-center justify-center gap-1 rounded-lg border border-gray-700 bg-gray-800 px-3 py-1 text-gray-100 sm:hidden"
          >
            <span className="h-0.5 w-5 rounded bg-current" />
            <span className="h-0.5 w-5 rounded bg-current" />
            <span className="h-0.5 w-5 rounded bg-current" />
          </button>
          <nav aria-label="Primary sections" className="hidden sm:flex gap-1 items-center">
            {tabs.map(t => (
              <button
                key={t}
                onClick={() => switchTopLevelSection(t)}
                className={`inline-flex items-center rounded-lg px-4 py-1 text-sm font-medium capitalize transition-colors
                  ${tab === t ? 'bg-gray-800 text-white' : 'text-gray-400 hover:text-gray-200'}`}
              >
                {t}
              </button>
            ))}
          </nav>
          {!chatOpen && (
            <button
              onClick={() => setChatOpen(true)}
              className="ml-auto inline-flex items-center justify-center rounded-lg bg-blue-600 px-4 py-1 text-sm font-medium text-white hover:bg-blue-500"
            >
              Ask AI
            </button>
          )}
        </header>

        <div role="region" aria-label="Filter bar" className="flex flex-wrap gap-4 px-4 py-2 border-b border-gray-800 items-center bg-gray-900">
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
            <MappingView month={month} division={division} />
          )}
          {tab === 'applications' && !selectedApp && (
            <ApplicationsView month={month} division={division} onSelectApp={setSelectedApp} />
          )}
          {tab === 'applications' && selectedApp && (
            <ApplicationDetail
              app={selectedApp}
              backLabel={appBackProduct ? `Back to ${appBackProduct.name}` : undefined}
              onBack={() => {
                setSelectedApp(null);
                if (appBackProduct) {
                  setTab('products');
                  setSelectedProduct(appBackProduct);
                  setAppBackProduct(null);
                }
              }}
              onNavigateToProduct={async (productId) => {
                const data: ProductRisk[] = await fetch(`/api/products?month=${month}`).then(r => r.json());
                const product = data.find(p => p.id === productId);
                if (product) {
                  setProductBackApp(selectedApp);
                  setTab('products');
                  setSelectedApp(null);
                  setSelectedProduct(product);
                }
              }}
            />
          )}
          {tab === 'products' && !selectedProduct && (
            <ProductsView month={month} division={division} onSelectProduct={setSelectedProduct} />
          )}
          {tab === 'projects' && (
            <ProjectsView month={month} division={division} />
          )}
          {tab === 'products' && selectedProduct && (
            <ProductDetail
              product={selectedProduct}
              backLabel={productBackApp ? `Back to ${productBackApp.name}` : undefined}
              onBack={() => {
                setSelectedProduct(null);
                if (productBackApp) {
                  setTab('applications');
                  setSelectedApp(productBackApp);
                  setProductBackApp(null);
                }
              }}
              onNavigateToApp={async (appId) => {
                const data: ApplicationRisk[] = await fetch(`/api/applications?month=${month}`).then(r => r.json());
                const app = data.find(a => a.id === appId);
                if (app) {
                  setAppBackProduct(selectedProduct);
                  setTab('applications');
                  setSelectedProduct(null);
                  setSelectedApp(app);
                }
              }}
            />
          )}
        </div>
      </div>

      <div
        className={`flex-shrink-0 flex flex-col bg-gray-900 border-l border-gray-700 transition-all duration-300 overflow-hidden
          ${chatOpen ? 'w-[420px]' : 'w-0'}`}
      >
        <ChatPanel month={month} onClose={() => setChatOpen(false)} />
      </div>
    </div>
  );
}
