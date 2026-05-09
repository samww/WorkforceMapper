import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import ProductsView from './ProductsView';

const PRODUCTS = [
  {
    id: 1, name: 'DataLicensing', arr: 6200000, risk: 'high',
    contributing_apps: [{ id: 4, name: 'AuthService', risk: 'high' }],
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
    render(<ProductsView month="2026-05" onSelectProduct={() => {}} />);
    expect(await screen.findByText('DataLicensing')).toBeInTheDocument();
    expect(screen.getByText('MediaMeasurement')).toBeInTheDocument();
  });

  it('shows a high-risk badge for DataLicensing', async () => {
    mockFetch();
    render(<ProductsView month="2026-05" onSelectProduct={() => {}} />);
    await screen.findByText('DataLicensing');
    expect(screen.getByText(/high/i)).toBeInTheDocument();
  });
});
