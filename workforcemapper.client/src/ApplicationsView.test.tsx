import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import ApplicationsView, { CoverageBar } from './ApplicationsView';

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

describe('CoverageBar', () => {
  it('fill width is current/barMax as a percentage', () => {
    render(<CoverageBar current={3} min={4} recommended={5} barMax={10} />);
    const fill = screen.getByTestId('coverage-fill');
    expect(fill.style.width).toBe('30%');
  });

  it('min marker left is min/barMax as a percentage', () => {
    render(<CoverageBar current={3} min={4} recommended={5} barMax={10} />);
    const minMarker = screen.getByTestId('coverage-min-marker');
    expect(minMarker.style.left).toBe('40%');
  });

  it('recommended marker left is recommended/barMax as a percentage', () => {
    render(<CoverageBar current={3} min={4} recommended={5} barMax={10} />);
    const recMarker = screen.getByTestId('coverage-rec-marker');
    expect(recMarker.style.left).toBe('50%');
  });
});

describe('ApplicationsView shared barMax', () => {
  it('all visible apps share one scale — fills are proportional to global max', async () => {
    // APPS barMax = max(3,4,5, 2.5,2,3, 3,2,3) = 5.0 (AuthService recommended)
    // AuthService fill = 3/5 = 60%; SurveyEngine fill = 2.5/5 = 50%
    mockFetch();
    render(<ApplicationsView month="2026-05" division="All" onSelectApp={() => {}} />);
    await screen.findByText('AuthService');
    const fills = screen.getAllByTestId('coverage-fill');
    // sorted: high→medium→low → AuthService[0], SurveyEngine[1], CoreDataWarehouse[2]
    expect(fills[0].style.width).toBe('60%');
    expect(fills[1].style.width).toBe('50%');
  });

  it('division filter recomputes barMax over visible set only', async () => {
    // Analytics only: CoreDataWarehouse current=3, min=2, recommended=3 → barMax=3 → fill 100%
    mockFetch();
    render(<ApplicationsView month="2026-05" division="Analytics" onSelectApp={() => {}} />);
    await screen.findByText('CoreDataWarehouse');
    const fill = screen.getByTestId('coverage-fill');
    expect(fill.style.width).toBe('100%');
  });
});

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
