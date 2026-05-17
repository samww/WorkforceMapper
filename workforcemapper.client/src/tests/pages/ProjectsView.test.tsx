import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import ProjectsView from '../../pages/ProjectsView';

const PROJECTS = [
  { id: 1, name: 'P03 Charlie', divisions: ['Analytics', 'Platform Engineering'], allocation_fte: 2.0 },
  { id: 2, name: 'P01 Alpha', divisions: ['Data Collection'], allocation_fte: 5.0 },
  { id: 3, name: 'P02 Bravo', divisions: ['Client Services', 'Analytics'], allocation_fte: 3.5 },
];

function mockFetch(data = PROJECTS) {
  return vi.spyOn(globalThis, 'fetch').mockImplementation(() =>
    Promise.resolve({ ok: true, json: () => Promise.resolve(data) } as Response)
  );
}

beforeEach(() => vi.restoreAllMocks());

describe('ProjectsView', () => {
  it('renders project names after fetch', async () => {
    mockFetch();
    render(<ProjectsView month="2026-05" division="All" />);
    expect(await screen.findByText('P01 Alpha')).toBeInTheDocument();
    expect(screen.getByText('P03 Charlie')).toBeInTheDocument();
  });

  it('shows spinner while loading', () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(() => new Promise(() => {}));
    render(<ProjectsView month="2026-05" division="All" />);
    expect(screen.getByRole('status')).toBeInTheDocument();
  });

  it('division filter shows only projects that include the division', async () => {
    mockFetch();
    render(<ProjectsView month="2026-05" division="Data Collection" />);
    expect(await screen.findByText('P01 Alpha')).toBeInTheDocument();
    expect(screen.queryByText('P03 Charlie')).not.toBeInTheDocument();
  });
});

describe('ProjectsView sort', () => {
  it('default sort is name asc (P01 → P02 → P03)', async () => {
    mockFetch();
    render(<ProjectsView month="2026-05" division="All" />);
    await screen.findByText('P01 Alpha');
    const rows = screen.getAllByRole('row');
    expect(rows[1]).toHaveTextContent('P01 Alpha');
    expect(rows[2]).toHaveTextContent('P02 Bravo');
    expect(rows[3]).toHaveTextContent('P03 Charlie');
  });

  it('clicking Allocation header sorts allocation desc (largest first)', async () => {
    mockFetch();
    render(<ProjectsView month="2026-05" division="All" />);
    await screen.findByText('P01 Alpha');
    fireEvent.click(screen.getByRole('button', { name: /allocation/i }));
    const rows = screen.getAllByRole('row');
    expect(rows[1]).toHaveTextContent('P01 Alpha');   // 5.0
    expect(rows[2]).toHaveTextContent('P02 Bravo');   // 3.5
    expect(rows[3]).toHaveTextContent('P03 Charlie'); // 2.0
  });

  it('clicking Allocation twice toggles to allocation asc', async () => {
    mockFetch();
    render(<ProjectsView month="2026-05" division="All" />);
    await screen.findByText('P01 Alpha');
    fireEvent.click(screen.getByRole('button', { name: /allocation/i }));
    fireEvent.click(screen.getByRole('button', { name: /allocation/i }));
    const rows = screen.getAllByRole('row');
    expect(rows[1]).toHaveTextContent('P03 Charlie'); // 2.0
    expect(rows[2]).toHaveTextContent('P02 Bravo');   // 3.5
    expect(rows[3]).toHaveTextContent('P01 Alpha');   // 5.0
  });

  it('clicking Divisions sorts by first division alphabetically asc', async () => {
    mockFetch();
    render(<ProjectsView month="2026-05" division="All" />);
    await screen.findByText('P01 Alpha');
    fireEvent.click(screen.getByRole('button', { name: /divisions/i }));
    const rows = screen.getAllByRole('row');
    // P02: sort(['Client Services','Analytics'])[0] = 'Analytics'
    // P03: sort(['Analytics','Platform Engineering'])[0] = 'Analytics'
    // P01: sort(['Data Collection'])[0] = 'Data Collection'
    // P02 and P03 tie on first-div='Analytics'; name asc tiebreaker: P02 Bravo < P03 Charlie
    expect(rows[1]).toHaveTextContent('P02 Bravo');
    expect(rows[2]).toHaveTextContent('P03 Charlie');
    expect(rows[3]).toHaveTextContent('P01 Alpha');
  });

  it('active column header shows sort arrow, inactive columns do not', async () => {
    mockFetch();
    render(<ProjectsView month="2026-05" division="All" />);
    await screen.findByText('P01 Alpha');
    // Default: Name is active (asc)
    expect(screen.getByRole('button', { name: /name/i }).textContent).toMatch(/[▲▼]/);
    expect(screen.getByRole('button', { name: /allocation/i }).textContent).not.toMatch(/[▲▼]/);
  });
});
