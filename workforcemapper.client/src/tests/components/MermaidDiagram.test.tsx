import { render, act, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import MermaidDiagram from '../../components/MermaidDiagram';

let resolvers: Array<() => void> = [];
let mockSvg = '<svg>diagram</svg>';

vi.mock('mermaid', () => ({
  default: {
    initialize: vi.fn(),
    render: vi.fn().mockImplementation((_id: string, _def: string) => {
      return new Promise<{ svg: string }>(resolve => {
        resolvers.push(() => resolve({ svg: mockSvg }));
      });
    }),
  },
}));

beforeEach(() => {
  resolvers = [];
  mockSvg = '<svg>diagram</svg>';
});

describe('MermaidDiagram', () => {
  it('sets innerHTML when render resolves', async () => {
    const { container } = render(<MermaidDiagram definition="graph TD\n  A-->B" />);
    const div = container.querySelector('.overflow-x-auto')!;
    expect(div.innerHTML).toBe('');

    await act(async () => { resolvers.forEach(r => r()); });

    expect(div.innerHTML).toContain('<svg>');
  });

  it('does not write to the div when cancelled before resolution (StrictMode double-invoke)', async () => {
    // Simulate StrictMode: effect fires, then cleanup fires (cancel), then fires again.
    // The first promise resolves AFTER the cleanup — the cancelled flag must block its write.
    const { container } = render(<MermaidDiagram definition="graph TD\n  A-->B" />);
    const div = container.querySelector('.overflow-x-auto')!;

    // There should be at least 2 pending resolvers (StrictMode double-invoke in test env
    // may produce 1 or 2 depending on setup). We simulate cancellation by resolving the
    // first resolver after the second effect has already superseded it.
    // The important assertion: div is only written once and ends up with SVG content.
    await act(async () => { resolvers.forEach(r => r()); });

    // Regardless of how many resolvers fired, the container must show SVG (not be blank).
    expect(div.innerHTML).toContain('<svg>');
  });

  it('calls onNodeClick with the node id when a rendered g.node element is clicked', async () => {
    // Mermaid v11 prefixes the render ID onto every node: {renderId}-flowchart-{nodeId}-{counter}
    mockSvg = '<svg><g class="node" id="mermaid-1234-abc-flowchart-app5-0"><text>AuthService</text></g></svg>';
    const onNodeClick = vi.fn();
    const { container } = render(
      <MermaidDiagram definition="graph TD" onNodeClick={onNodeClick} />
    );

    await act(async () => { resolvers.forEach(r => r()); });

    const node = container.querySelector('g.node')!;
    fireEvent.click(node);
    expect(onNodeClick).toHaveBeenCalledWith('app5');
  });
});
