import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import ChatPanel from './ChatPanel';

const DEFAULT_MONTH = '2026-05';

function makeSSEStream(...tokens: string[]) {
  const encoder = new TextEncoder();
  return new ReadableStream<Uint8Array>({
    start(controller) {
      for (const token of tokens) {
        controller.enqueue(encoder.encode(`data: ${JSON.stringify(token)}\n\n`));
      }
      controller.enqueue(encoder.encode('data: [DONE]\n\n'));
      controller.close();
    },
  });
}

beforeEach(() => {
  vi.restoreAllMocks();
});

describe('ChatPanel', () => {
  it('clicking a chip populates the textarea with its query text', async () => {
    render(<ChatPanel />);
    await userEvent.click(
      screen.getByRole('button', { name: /why is authservice at risk/i })
    );
    expect(screen.getByRole('textbox')).toHaveValue(
      'Why is AuthService at risk this month and where could we find the capacity to fix it?'
    );
  });

  it('submitting a chip query POSTs the correct body to /api/chat', async () => {
    let capturedBody: string | undefined;
    vi.spyOn(globalThis, 'fetch').mockImplementation((_url, opts) => {
      capturedBody = (opts as RequestInit)?.body as string;
      return Promise.resolve({ ok: true, body: makeSSEStream() } as unknown as Response);
    });

    render(<ChatPanel />);
    await userEvent.click(
      screen.getByRole('button', { name: /why is authservice at risk/i })
    );
    await userEvent.click(screen.getByRole('button', { name: /send/i }));

    expect(capturedBody).toBeDefined();
    const body = JSON.parse(capturedBody!);
    expect(body.query).toBe('Why is AuthService at risk this month and where could we find the capacity to fix it?');
    expect(body.month).toBe(DEFAULT_MONTH);
  });

  it('renders streamed text tokens concatenated in the response area', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue({
      ok: true,
      body: makeSSEStream('Data Collection has ', '$18,000 unallocated.'),
    } as unknown as Response);

    render(<ChatPanel />);
    await userEvent.type(screen.getByRole('textbox'), 'test query');
    await userEvent.click(screen.getByRole('button', { name: /send/i }));

    await waitFor(() =>
      expect(
        screen.getByText('Data Collection has $18,000 unallocated.')
      ).toBeInTheDocument()
    );
  });

  it('shows the submitted query above the response', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue({
      ok: true,
      body: makeSSEStream('Some answer.'),
    } as unknown as Response);

    render(<ChatPanel />);
    await userEvent.type(screen.getByRole('textbox'), 'my question');
    await userEvent.click(screen.getByRole('button', { name: /send/i }));

    await waitFor(() => expect(screen.getByText('Some answer.')).toBeInTheDocument());
    expect(screen.getByText('my question')).toBeInTheDocument();
  });

  it('"New question" resets to the chips view', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue({
      ok: true,
      body: makeSSEStream('Some answer.'),
    } as unknown as Response);

    render(<ChatPanel />);
    await userEvent.type(screen.getByRole('textbox'), 'my question');
    await userEvent.click(screen.getByRole('button', { name: /send/i }));
    await waitFor(() => expect(screen.getByText('Some answer.')).toBeInTheDocument());

    await userEvent.click(screen.getByRole('button', { name: /new question/i }));

    expect(screen.queryByText('Some answer.')).not.toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: /why is authservice at risk/i })
    ).toBeInTheDocument();
  });

  it('shows ToolCallIndicator while awaiting first token, hides it after', async () => {
    let resolveFetch!: (r: Response) => void;
    vi.spyOn(globalThis, 'fetch').mockImplementation(
      () => new Promise<Response>(resolve => { resolveFetch = resolve; })
    );

    render(<ChatPanel />);
    await userEvent.type(screen.getByRole('textbox'), 'test query');
    await userEvent.click(screen.getByRole('button', { name: /send/i }));

    expect(screen.getByText('Checking capacity by division…')).toBeInTheDocument();

    resolveFetch({ ok: true, body: makeSSEStream('Hello') } as unknown as Response);

    await waitFor(() =>
      expect(
        screen.queryByText('Checking capacity by division…')
      ).not.toBeInTheDocument()
    );
    expect(screen.getByText('Hello')).toBeInTheDocument();
  });
});
