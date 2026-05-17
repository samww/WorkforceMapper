import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import App from './App';

function mockFetch() {
  vi.spyOn(globalThis, 'fetch').mockImplementation(() =>
    Promise.resolve({ ok: true, json: () => Promise.resolve([]) } as Response)
  );
}

function mockFetchRoutes() {
  vi.spyOn(globalThis, 'fetch').mockImplementation((input) => {
    const url = String(input);
    const applications = [
      {
        id: 1,
        name: 'CoreDataWarehouse',
        division: 'Analytics',
        min_support_fte: 2,
        recommended_support_fte: 3,
        current_coverage_fte: 2.5,
        risk: 'medium',
        dependent_products: [],
        transitive_dependent_apps: [],
        dependent_app_count: 0,
      },
    ];

    const data = url.startsWith('/api/applications') ? applications : [];
    return Promise.resolve({ ok: true, json: () => Promise.resolve(data) } as Response);
  });
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

  it('matches the vertical height of the nav bar — neither bar forces min-h-16', () => {
    mockFetch();
    render(<App />);
    expect(screen.getByRole('banner')).not.toHaveClass('min-h-16');
    expect(screen.getByRole('region', { name: /filter bar/i })).not.toHaveClass('min-h-16');
  });
});

describe('Ask AI button', () => {
  it('is rendered as a global control outside the section navigation', () => {
    mockFetch();
    render(<App />);
    const sectionNav = screen.getByRole('navigation', { name: /primary sections/i });
    expect(screen.getByRole('button', { name: /ask ai/i })).toBeInTheDocument();
    expect(within(sectionNav).queryByRole('button', { name: /ask ai/i })).not.toBeInTheDocument();
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

describe('Responsive navigation', () => {
  it('keeps section tabs on desktop and exposes a mobile hamburger trigger', () => {
    mockFetch();
    render(<App />);

    expect(screen.getByRole('banner')).toHaveClass('items-center');
    expect(screen.getByRole('banner')).toHaveClass('py-2');

    const desktopNav = screen.getByRole('navigation', { name: /primary sections/i });
    expect(desktopNav).toHaveClass('hidden');
    expect(desktopNav).toHaveClass('sm:flex');
    expect(within(desktopNav).getByRole('button', { name: /mappings/i })).toHaveClass('rounded-lg');
    expect(within(desktopNav).getByRole('button', { name: /applications/i })).toHaveClass('rounded-lg');
    expect(within(desktopNav).getByRole('button', { name: /products/i })).toHaveClass('rounded-lg');
    expect(within(desktopNav).getByRole('button', { name: /projects/i })).toHaveClass('rounded-lg');

    const mobileTrigger = screen.getByRole('button', { name: /open navigation/i });
    expect(mobileTrigger).toHaveClass('sm:hidden');
    expect(mobileTrigger).toHaveClass('items-center');
    expect(screen.getByRole('button', { name: /ask ai/i })).toHaveClass('items-center');
  });

  it('opens a left drawer with top-level destinations from the mobile trigger', async () => {
    mockFetch();
    render(<App />);

    await userEvent.click(screen.getByRole('button', { name: /open navigation/i }));

    const drawer = screen.getByRole('dialog', { name: /section navigation/i });
    expect(drawer).toHaveClass('w-[min(20rem,85vw)]');
    expect(within(drawer).getByRole('button', { name: /mappings/i })).toBeInTheDocument();
    expect(within(drawer).getByRole('button', { name: /applications/i })).toBeInTheDocument();
    expect(within(drawer).getByRole('button', { name: /products/i })).toBeInTheDocument();
    expect(within(drawer).getByRole('button', { name: /projects/i })).toBeInTheDocument();
    expect(within(drawer).queryByRole('button', { name: /ask ai/i })).not.toBeInTheDocument();
    expect(within(drawer).queryByLabelText(/month/i)).not.toBeInTheDocument();
    expect(within(drawer).queryByLabelText(/division/i)).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /ask ai/i })).toBeInTheDocument();
    expect(screen.getByLabelText(/month/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/division/i)).toBeInTheDocument();
  });

  it('closes the mobile drawer when Escape is pressed', async () => {
    mockFetch();
    render(<App />);

    await userEvent.click(screen.getByRole('button', { name: /open navigation/i }));
    expect(screen.getByRole('dialog', { name: /section navigation/i })).toBeInTheDocument();

    await userEvent.keyboard('{Escape}');

    expect(screen.queryByRole('dialog', { name: /section navigation/i })).not.toBeInTheDocument();
  });

  it('closes the mobile drawer from the in-drawer close button and backdrop', async () => {
    mockFetch();
    render(<App />);

    await userEvent.click(screen.getByRole('button', { name: /open navigation/i }));
    await userEvent.click(screen.getByRole('button', { name: /^close navigation$/i }));
    expect(screen.queryByRole('dialog', { name: /section navigation/i })).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: /open navigation/i }));
    await userEvent.click(screen.getByRole('button', { name: /close navigation backdrop/i }));
    expect(screen.queryByRole('dialog', { name: /section navigation/i })).not.toBeInTheDocument();
  });

  it('switches sections through the mobile drawer and clears detail-screen state', async () => {
    mockFetchRoutes();
    render(<App />);

    await userEvent.click(screen.getByRole('button', { name: /open navigation/i }));
    await userEvent.click(within(screen.getByRole('dialog', { name: /section navigation/i })).getByRole('button', { name: /applications/i }));
    await screen.findByText('CoreDataWarehouse');
    await userEvent.click(screen.getByText('CoreDataWarehouse'));
    expect(screen.getByRole('button', { name: /back to applications/i })).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: /open navigation/i }));
    await userEvent.click(within(screen.getByRole('dialog', { name: /section navigation/i })).getByRole('button', { name: /projects/i }));

    expect(screen.queryByRole('dialog', { name: /section navigation/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /back to applications/i })).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: /open navigation/i }));
    await userEvent.click(within(screen.getByRole('dialog', { name: /section navigation/i })).getByRole('button', { name: /applications/i }));

    expect(await screen.findByRole('heading', { name: /applications/i })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /back to applications/i })).not.toBeInTheDocument();
  });
});
