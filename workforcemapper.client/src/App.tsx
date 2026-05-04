import { useState } from 'react';
import MappingView from './MappingView';
import ChatPanel from './ChatPanel';

export default function App() {
  const [chatOpen, setChatOpen] = useState(false);

  return (
    <div className="flex h-screen overflow-hidden">
      <div className="flex-1 overflow-auto min-w-0">
        <MappingView onOpenChat={chatOpen ? undefined : () => setChatOpen(true)} />
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
