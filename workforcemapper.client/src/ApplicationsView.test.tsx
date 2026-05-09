import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import ApplicationsView from './ApplicationsView';

const APPS = [
  { id: 4, name: 'AuthService', division: 'Platform Engineering', min_support_fte: 4.0, recommended_support_fte: 5.0, current_coverage_fte: 3.0, risk: 'high', dependent_products: [] },
  { id: 3, name: 'SurveyEngine', division: 'Platform Engineering', min_support_fte: 2.0, recommended_support_fte: 3.0, current_coverage_fte: 2.5, risk: 'medium', dependent_products: [] },
  { id: 1, name: 'CoreDataWarehouse', division: 'Analytics', min_support_fte: 2.0, recommended_support_fte: 3.0, current_coverage_fte: 3.0, risk: 'low', dependent_products: [] },
];

function mockFetch(apps = APPS) {
  return vi.spyOn(globalThis, 'fetch').mockImplementation(() =>
    Promise.resolve({ ok: true, json: () => Promise.resolve(apps) } as Response)
  );
}

beforeEach(() => vi.restoreAllMocks());

describe('ApplicationsView', () => {
  it('renders without crashing and shows app names after fetch', async () => {
    mockFetch();
    render(<ApplicationsView month="2026-05" division="All" onSelectApp={() => {}} />);
    expect(await screen.findByText('AuthService')).toBeInTheDocument();
    expect(screen.getByText('SurveyEngine')).toBeInTheDocument();
    expect(screen.getByText('CoreDataWarehouse')).toBeInTheDocument();
  });

  it('shows a high-risk badge for AuthService', async () => {
    mockFetch();
    render(<ApplicationsView month="2026-05" division="All" onSelectApp={() => {}} />);
    await screen.findByText('AuthService');
    expect(screen.getByText(/high/i)).toBeInTheDocument();
  });

  it('division prop filters apps to matching division only', async () => {
    mockFetch();
    render(<ApplicationsView month="2026-05" division="Analytics" onSelectApp={() => {}} />);
    expect(await screen.findByText('CoreDataWarehouse')).toBeInTheDocument();
    expect(screen.queryByText('AuthService')).not.toBeInTheDocument();
    expect(screen.queryByText('SurveyEngine')).not.toBeInTheDocument();
  });

  it('shows a spinner while loading', () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(() => new Promise(() => {}));
    render(<ApplicationsView month="2026-05" division="All" onSelectApp={() => {}} />);
    expect(screen.getByRole('status')).toBeInTheDocument();
  });
});
