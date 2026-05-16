import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import App from './App';

function mockFetch() {
  vi.spyOn(globalThis, 'fetch').mockImplementation(() =>
    Promise.resolve({ ok: true, json: () => Promise.resolve([]) } as Response)
  );
}

beforeEach(() => {
  mockFetch();
  vi.restoreAllMocks();
});

describe('App filter bar', () => {
  it('renders a month select and a division select', () => {
    mockFetch();
    render(<App />);
    expect(screen.getByLabelText(/month/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/division/i)).toBeInTheDocument();
  });
});

describe('Ask AI button', () => {
  it('is rendered inside the nav element', () => {
    mockFetch();
    render(<App />);
    const nav = screen.getByRole('navigation');
    expect(within(nav).getByRole('button', { name: /ask ai/i })).toBeInTheDocument();
  });

  it('disappears when the chat panel opens', async () => {
    mockFetch();
    render(<App />);
    await userEvent.click(screen.getByRole('button', { name: /ask ai/i }));
    expect(screen.queryByRole('button', { name: /ask ai/i })).not.toBeInTheDocument();
  });

  it('reappears when the chat panel is closed via the close button', async () => {
    mockFetch();
    render(<App />);
    await userEvent.click(screen.getByRole('button', { name: /ask ai/i }));
    await userEvent.click(screen.getByRole('button', { name: /close chat/i }));
    expect(screen.getByRole('button', { name: /ask ai/i })).toBeInTheDocument();
  });
});
