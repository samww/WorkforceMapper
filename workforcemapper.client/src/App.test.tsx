import { render, screen } from '@testing-library/react';
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
