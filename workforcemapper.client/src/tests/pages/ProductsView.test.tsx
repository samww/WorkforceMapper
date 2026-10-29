import { render, screen, fireEvent, waitFor, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import ProductsView from '../../pages/ProductsView';

const PRODUCTS = [
  {
    id: 1, name: 'DataLicensing', arr: 6200000, risk: 'high',
    contributing_apps: [{ id: 4, name: 'AuthService', risk: 'high', division: 'Platform Engineering' }],
    direct_apps: [{ id: 4, name: 'AuthService', risk: 'high', dependencies: [] }],
  },
  {
    id: 2, name: 'MediaMeasurement', arr: 5100000, risk: 'medium',
    contributing_apps: [],
    direct_apps: [],
  },
];

function mockFetch(data = PRODUCTS) {
  return vi.spyOn(globalThis, 'fetch').mockImplementation(() =>
    Promise.resolve({ ok: true, json: () => Promise.resolve(data) } as Response)
  );
}

beforeEach(() => vi.restoreAllMocks());

describe('ProductsView', () => {
  it('renders without crashing and shows product names after fetch', async () => {
    mockFetch();
    render(<ProductsView month="2026-05" division="All" onSelectProduct={() => {}} />);
    expect(await screen.findByText('DataLicensing')).toBeInTheDocument();
    expect(screen.getByText('MediaMeasurement')).toBeInTheDocument();
  });

  it('shows a high-risk badge for DataLicensing', async () => {
    mockFetch();
    render(<ProductsView month="2026-05" division="All" onSelectProduct={() => {}} />);
    await screen.findByText('DataLicensing');
    expect(screen.getByText(/high/i)).toBeInTheDocument();
  });

  it('division prop shows only products with a contributing app in that division', async () => {
    mockFetch();
    render(<ProductsView month="2026-05" division="Platform Engineering" onSelectProduct={() => {}} />);
    expect(await screen.findByText('DataLicensing')).toBeInTheDocument();
    expect(screen.queryByText('MediaMeasurement')).not.toBeInTheDocument();
  });

  it('shows a spinner while loading', () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(() => new Promise(() => {}));
    render(<ProductsView month="2026-05" division="All" onSelectProduct={() => {}} />);
    expect(screen.getByRole('status')).toBeInTheDocument();
  });
});

const SORT_PRODUCTS = [
  { id: 1, name: 'Zeta', arr: 3000000, risk: 'high' as const, contributing_apps: [], direct_apps: [] },
  { id: 2, name: 'Alpha', arr: 6000000, risk: 'high' as const, contributing_apps: [], direct_apps: [] },
  { id: 3, name: 'Mango', arr: 1000000, risk: 'low' as const, contributing_apps: [], direct_apps: [] },
];

describe('ProductsView sort', () => {
  it('default sort is risk-desc then name-asc tiebreaker', async () => {
    mockFetch(SORT_PRODUCTS);
    render(<ProductsView month="2026-05" division="All" onSelectProduct={() => {}} />);
    await screen.findByText('Alpha');
    const rows = screen.getAllByRole('row');
    expect(rows[1]).toHaveTextContent('Alpha');  // high, A < Z
    expect(rows[2]).toHaveTextContent('Zeta');   // high, Z
    expect(rows[3]).toHaveTextContent('Mango');  // low
  });

  it('clicking Name header sorts by name asc', async () => {
    mockFetch(SORT_PRODUCTS);
    render(<ProductsView month="2026-05" division="All" onSelectProduct={() => {}} />);
    await screen.findByText('Alpha');
    fireEvent.click(screen.getByRole('button', { name: /name/i }));
    const rows = screen.getAllByRole('row');
    expect(rows[1]).toHaveTextContent('Alpha');
    expect(rows[2]).toHaveTextContent('Mango');
    expect(rows[3]).toHaveTextContent('Zeta');
  });

  it('clicking Name header twice toggles to name desc', async () => {
    mockFetch(SORT_PRODUCTS);
    render(<ProductsView month="2026-05" division="All" onSelectProduct={() => {}} />);
    await screen.findByText('Alpha');
    fireEvent.click(screen.getByRole('button', { name: /name/i }));
    fireEvent.click(screen.getByRole('button', { name: /name/i }));
    const rows = screen.getAllByRole('row');
    expect(rows[1]).toHaveTextContent('Zeta');
    expect(rows[2]).toHaveTextContent('Mango');
    expect(rows[3]).toHaveTextContent('Alpha');
  });

  it('clicking ARR header (first click) sorts arr desc (largest first)', async () => {
    mockFetch(SORT_PRODUCTS);
    render(<ProductsView month="2026-05" division="All" onSelectProduct={() => {}} />);
    await screen.findByText('Alpha');
    fireEvent.click(screen.getByRole('button', { name: /arr/i }));
    const rows = screen.getAllByRole('row');
    expect(rows[1]).toHaveTextContent('Alpha');  // 6M
    expect(rows[2]).toHaveTextContent('Zeta');   // 3M
    expect(rows[3]).toHaveTextContent('Mango');  // 1M
  });

  it('clicking Risk header (first click) sorts risk desc', async () => {
    mockFetch(SORT_PRODUCTS);
    render(<ProductsView month="2026-05" division="All" onSelectProduct={() => {}} />);
    await screen.findByText('Alpha');
    fireEvent.click(screen.getByRole('button', { name: /name/i }));
    fireEvent.click(screen.getByRole('button', { name: /risk/i }));
    const rows = screen.getAllByRole('row');
    expect(rows[1]).toHaveTextContent('Alpha');  // high, A < Z
    expect(rows[2]).toHaveTextContent('Zeta');   // high
    expect(rows[3]).toHaveTextContent('Mango');  // low
  });

  it('active column header shows sort arrow, inactive columns do not', async () => {
    mockFetch(SORT_PRODUCTS);
    render(<ProductsView month="2026-05" division="All" onSelectProduct={() => {}} />);
    await screen.findByText('Alpha');
    // Default: Risk is active (desc)
    expect(screen.getByRole('button', { name: /risk/i }).textContent).toMatch(/[▲▼]/);
    expect(screen.getByRole('button', { name: /name/i }).textContent).not.toMatch(/[▲▼]/);
  });

  it('active column th has aria-sort attribute', async () => {
    mockFetch(SORT_PRODUCTS);
    render(<ProductsView month="2026-05" division="All" onSelectProduct={() => {}} />);
    await screen.findByText('Alpha');
    fireEvent.click(screen.getByRole('button', { name: /arr/i }));
    expect(screen.getByRole('button', { name: /arr/i }).closest('th')).toHaveAttribute('aria-sort', 'descending');
  });
});

describe('ProductsView month change', () => {
  it('shows the spinner and fetches the new month when month changes', async () => {
    const fetchSpy = mockFetch();
    const { rerender } = render(<ProductsView month="2026-05" division="All" onSelectProduct={() => {}} />);
    await screen.findByText('DataLicensing');

    fetchSpy.mockImplementation(() => new Promise(() => {}));
    rerender(<ProductsView month="2026-06" division="All" onSelectProduct={() => {}} />);

    expect(screen.getByRole('status')).toBeInTheDocument();
    expect(fetchSpy).toHaveBeenLastCalledWith('/api/products?month=2026-06');
  });

  it('replaces the old month with the new month once it loads', async () => {
    mockFetch();
    const { rerender } = render(<ProductsView month="2026-05" division="All" onSelectProduct={() => {}} />);
    await screen.findByText('DataLicensing');

    mockFetch([PRODUCTS[1]]);
    rerender(<ProductsView month="2026-06" division="All" onSelectProduct={() => {}} />);

    await waitFor(() => expect(screen.queryByRole('status')).not.toBeInTheDocument());
    expect(screen.getByText('MediaMeasurement')).toBeInTheDocument();
    expect(screen.queryByText('DataLicensing')).not.toBeInTheDocument();
  });
});

function deferFetchByMonth(immediate: Record<string, unknown> = {}) {
  const pending: Record<string, Array<(data: unknown) => void>> = {};
  vi.spyOn(globalThis, 'fetch').mockImplementation((url) => {
    const u = String(url);
    const hit = Object.keys(immediate).find(k => u.includes(k));
    if (hit) return Promise.resolve({ ok: true, json: () => Promise.resolve(immediate[hit]) } as Response);
    const month = u.match(/month=([\d-]+)/)?.[1] ?? '';
    return new Promise<Response>(resolve => {
      (pending[month] ??= []).push(data => resolve({ ok: true, json: () => Promise.resolve(data) } as Response));
    });
  });
  return async (month: string, data: unknown) => {
    await act(async () => { (pending[month] ?? []).splice(0).forEach(r => r(data)); });
  };
}

describe('ProductsView out-of-order responses', () => {
  it('keeps the selected month when an earlier month responds last', async () => {
    const release = deferFetchByMonth();
    const { rerender } = render(<ProductsView month="2026-06" division="All" onSelectProduct={() => {}} />);
    rerender(<ProductsView month="2026-07" division="All" onSelectProduct={() => {}} />);

    await release('2026-07', [PRODUCTS[0]]);
    await release('2026-06', [PRODUCTS[1]]);

    expect(screen.queryByRole('status')).not.toBeInTheDocument();
    expect(screen.getByText('DataLicensing')).toBeInTheDocument();
    expect(screen.queryByText('MediaMeasurement')).not.toBeInTheDocument();
  });
});
