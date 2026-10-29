import { useEffect, useRef } from 'react';
import mermaid from 'mermaid';

mermaid.initialize({ startOnLoad: false, theme: 'dark' });

interface Props {
  definition: string;
  onNodeClick?: (nodeId: string) => void;
}

export default function MermaidDiagram({ definition, onNodeClick }: Props) {
  const ref = useRef<HTMLDivElement>(null);
  const onNodeClickRef = useRef(onNodeClick);

  useEffect(() => {
    onNodeClickRef.current = onNodeClick;
  }, [onNodeClick]);

  useEffect(() => {
    if (!ref.current) return;
    // Fresh unique ID per invocation: prevents mermaid from finding a stale
    // same-ID element in the DOM and removing it (StrictMode double-invoke race).
    const renderId = `mermaid-${Date.now()}-${Math.random().toString(36).slice(2)}`;
    let cancelled = false;
    mermaid.render(renderId, definition).then(({ svg }) => {
      if (!cancelled && ref.current) {
        ref.current.innerHTML = svg;
        if (onNodeClickRef.current) {
          ref.current.querySelectorAll<SVGGElement>('g.node').forEach(node => {
            const match = node.id?.match(/flowchart-(.+)-\d+$/);
            const nodeId = match?.[1];
            if (nodeId) {
              node.style.cursor = 'pointer';
              node.addEventListener('click', () => onNodeClickRef.current?.(nodeId));
            }
          });
        }
      }
    });
    return () => { cancelled = true; };
  }, [definition]);

  return <div ref={ref} className="overflow-x-auto" />;
}
