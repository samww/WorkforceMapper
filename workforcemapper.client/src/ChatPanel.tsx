import { useState } from 'react';
import ToolCallIndicator from './ToolCallIndicator';

const CHIPS = [
  'Why is AuthService at risk this month and where could we find the capacity to fix it?',
  'What unallocated resource cost does Data Collection carry this month?',
  'What is the monthly cost attribution by product?',
  'Which division is most constrained on capacity this month?',
];

interface Props {
  month: string;
  onClose?: () => void;
}

function parseInline(content: string) {
  return content.split(/(\*\*[^*]+\*\*)/g).map((part, j) => {
    if (part.startsWith('**') && part.endsWith('**')) {
      return <strong key={j}>{part.slice(2, -2)}</strong>;
    }
    return part;
  });
}

function isTableSeparator(line: string) {
  return /^\|[-| :]+\|$/.test(line.trim());
}

function splitTableRow(line: string) {
  return line.trim().replace(/^\||\|$/g, '').split('|').map(c => c.trim());
}

function renderMarkdown(text: string) {
  const lines = text.split('\n');
  const output: React.ReactNode[] = [];
  let i = 0;

  while (i < lines.length) {
    const line = lines[i];

    // Table: current line is a pipe row and next line is a separator
    if (line.trim().startsWith('|') && lines[i + 1] && isTableSeparator(lines[i + 1])) {
      const headers = splitTableRow(line);
      i += 2; // skip header + separator
      const rows: string[][] = [];
      while (i < lines.length && lines[i].trim().startsWith('|')) {
        rows.push(splitTableRow(lines[i]));
        i++;
      }
      output.push(
        <div key={i} className="overflow-x-auto my-2">
          <table className="min-w-full text-xs border-collapse">
            <thead>
              <tr>
                {headers.map((h, j) => (
                  <th key={j} className="border border-gray-600 bg-gray-800 px-3 py-1 text-left font-semibold text-gray-200">
                    {parseInline(h)}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {rows.map((row, ri) => (
                <tr key={ri} className={ri % 2 === 0 ? 'bg-gray-900' : 'bg-gray-850'}>
                  {row.map((cell, ci) => (
                    <td key={ci} className="border border-gray-700 px-3 py-1 text-gray-300">
                      {parseInline(cell)}
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      );
      continue;
    }

    const isBullet = /^[-*]\s/.test(line);
    const content = line.replace(/^[-*]\s/, '');
    const parts = parseInline(content);

    if (isBullet) output.push(<li key={i} className="ml-4 list-disc">{parts}</li>);
    else if (line.trim() === '') output.push(<br key={i} />);
    else output.push(<span key={i} className="block">{parts}</span>);

    i++;
  }

  return output;
}

export default function ChatPanel({ month, onClose }: Props) {
  const [query, setQuery] = useState('');
  const [submittedQuery, setSubmittedQuery] = useState('');
  const [response, setResponse] = useState('');
  const [isStreaming, setIsStreaming] = useState(false);
  const [isToolCalling, setIsToolCalling] = useState(false);

  async function submit(q: string) {
    const trimmed = q.trim();
    if (!trimmed || isStreaming) return;

    setSubmittedQuery(trimmed);
    setQuery('');
    setResponse('');
    setIsStreaming(true);
    setIsToolCalling(true);

    const res = await fetch('/api/chat', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ query: trimmed, month }),
    });

    if (!res.ok || !res.body) {
      setIsStreaming(false);
      setIsToolCalling(false);
      return;
    }

    const reader = res.body.getReader();
    const decoder = new TextDecoder();
    let buffer = '';

    outer: while (true) {
      const { done, value } = await reader.read();
      if (done) break;
      buffer += decoder.decode(value, { stream: true });
      const lines = buffer.split('\n');
      buffer = lines.pop() ?? '';
      for (const line of lines) {
        if (!line.startsWith('data: ')) continue;
        const data = line.slice(6);
        if (data === '[DONE]') break outer;
        setIsToolCalling(false);
        setResponse(prev => prev + JSON.parse(data));
      }
    }

    setIsStreaming(false);
  }

  function reset() {
    setQuery('');
    setSubmittedQuery('');
    setResponse('');
    setIsStreaming(false);
    setIsToolCalling(false);
  }

  function handleKeyDown(e: React.KeyboardEvent<HTMLTextAreaElement>) {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      submit(query);
    }
  }

  const showChips = !submittedQuery && !isStreaming;

  return (
    <div className="flex flex-col h-full bg-gray-900 text-gray-100">
      <div className="flex items-center justify-between px-4 py-3 border-b border-gray-700">
        <h2 className="text-sm font-semibold">Ask AI</h2>
        <div className="flex items-center gap-3">
          {(submittedQuery || response) && (
            <button
              onClick={reset}
              className="text-xs text-blue-400 hover:text-blue-300"
              aria-label="New question"
            >
              New question
            </button>
          )}
          {onClose && (
            <button
              onClick={onClose}
              className="text-gray-400 hover:text-gray-200 text-lg leading-none"
              aria-label="Close chat"
            >
              ✕
            </button>
          )}
        </div>
      </div>

      <div className="flex-1 overflow-y-auto px-4 py-3 space-y-3">
        {submittedQuery && (
          <p className="text-xs text-gray-400 italic">{submittedQuery}</p>
        )}

        {isToolCalling && <ToolCallIndicator />}

        {response && (
          <div className="text-sm text-gray-200 leading-relaxed space-y-1">
            {renderMarkdown(response)}
          </div>
        )}

        {showChips && (
          <div className="space-y-2 mt-4">
            <p className="text-xs text-gray-500 mb-3">Try asking:</p>
            {CHIPS.map(chip => (
              <button
                key={chip}
                onClick={() => setQuery(chip)}
                className="w-full text-left text-xs text-gray-300 bg-gray-800 hover:bg-gray-700 border border-gray-600 rounded-lg px-3 py-2 transition-colors"
              >
                {chip}
              </button>
            ))}
          </div>
        )}
      </div>

      <div className="px-4 py-3 border-t border-gray-700 flex gap-2">
        <textarea
          value={query}
          onChange={e => setQuery(e.target.value)}
          onKeyDown={handleKeyDown}
          placeholder="Ask a question about workforce capacity…"
          rows={2}
          className="flex-1 bg-gray-800 border border-gray-600 rounded-lg px-3 py-2 text-sm resize-none focus:outline-none focus:border-blue-500"
        />
        <button
          onClick={() => submit(query)}
          disabled={isStreaming}
          aria-label="Send"
          className="bg-blue-600 hover:bg-blue-500 disabled:opacity-50 rounded-lg px-3 py-2 text-sm font-medium self-end"
        >
          Send
        </button>
      </div>
    </div>
  );
}
