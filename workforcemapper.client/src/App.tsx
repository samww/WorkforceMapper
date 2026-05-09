import { useState } from 'react';
import MappingView from './MappingView';
import ChatPanel from './ChatPanel';
import ApplicationsView from './ApplicationsView';
import type { ApplicationRisk } from './ApplicationsView';
import ApplicationDetail from './ApplicationDetail';

type Tab = 'mappings' | 'applications';

const DEFAULT_MONTH = '2026-05';

export default function App() {
  const [chatOpen, setChatOpen] = useState(false);
  const [tab, setTab] = useState<Tab>('mappings');
  const [selectedApp, setSelectedApp] = useState<ApplicationRisk | null>(null);

  return (
    <div className="flex h-screen overflow-hidden">
      <div className="flex-1 overflow-auto min-w-0 flex flex-col">
        <nav className="flex gap-1 px-4 pt-4 pb-0 border-b border-gray-700">
          {(['mappings', 'applications'] as Tab[]).map(t => (
            <button
              key={t}
              onClick={() => { setTab(t); setSelectedApp(null); }}
              className={`px-4 py-2 text-sm font-medium capitalize rounded-t transition-colors
                ${tab === t ? 'bg-gray-800 text-white' : 'text-gray-400 hover:text-gray-200'}`}
            >
              {t}
            </button>
          ))}
        </nav>

        <div className="flex-1 overflow-auto">
          {tab === 'mappings' && (
            <MappingView onOpenChat={chatOpen ? undefined : () => setChatOpen(true)} />
          )}
          {tab === 'applications' && !selectedApp && (
            <ApplicationsView month={DEFAULT_MONTH} onSelectApp={setSelectedApp} />
          )}
          {tab === 'applications' && selectedApp && (
            <ApplicationDetail app={selectedApp} onBack={() => setSelectedApp(null)} />
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
