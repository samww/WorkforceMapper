import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import MappingView from './MappingView';

const RESOURCES = [
  {
    id: 1,
    resource_id: 'RES-001',
    name: 'Alice Chen',
    role_type: 'Engineer',
    seniority: 'Senior',
    resource_type: 'Employee',
    country: 'AU',
    division: 'Platform Engineering',
    monthly_rate: 15000,
    active: true,
    mappings: [{ activity_id: 1, activity_name: 'AuthService — Application Support', percent: 60 }],
  },
  {
    id: 2,
    resource_id: 'RES-002',
    name: 'Bob Smith',
    role_type: 'Analyst',
    seniority: 'Mid',
    resource_type: 'Contractor',
    country: 'US',
    division: 'Analytics',
    monthly_rate: 12000,
    active: true,
    mappings: [{ activity_id: 2, activity_name: 'CoreDataWarehouse — Application Support', percent: 100 }],
  },
];

const ACTIVITIES = [
  { id: 1, name: 'AuthService — Application Support', activity_type: 'Application Support' },
  { id: 2, name: 'CoreDataWarehouse — Application Support', activity_type: 'Application Support' },
];

function mockFetch(resources = RESOURCES, activities = ACTIVITIES) {
  return vi.spyOn(globalThis, 'fetch').mockImplementation((url) => {
    const u = String(url);
    if (u.includes('/api/resources')) {
      return Promise.resolve({ ok: true, json: () => Promise.resolve(resources) } as Response);
    }
    if (u.includes('/api/activities')) {
      return Promise.resolve({ ok: true, json: () => Promise.resolve(activities) } as Response);
    }
    return Promise.resolve({ ok: false } as Response);
  });
}

beforeEach(() => {
  vi.restoreAllMocks();
});

describe('MappingView', () => {
  it('renders a card for each resource', async () => {
    mockFetch();
    render(<MappingView />);
    expect(await screen.findByText('Alice Chen')).toBeInTheDocument();
    expect(screen.getByText('Bob Smith')).toBeInTheDocument();
  });

  it('division filter shows only cards in the selected division', async () => {
    mockFetch();
    render(<MappingView />);
    await screen.findByText('Alice Chen');

    await userEvent.selectOptions(screen.getByRole('combobox', { name: /division/i }), 'Analytics');

    expect(screen.queryByText('Alice Chen')).not.toBeInTheDocument();
    expect(screen.getByText('Bob Smith')).toBeInTheDocument();
  });

  it('unallocated filter hides resources with total allocation >= 100%', async () => {
    mockFetch();
    render(<MappingView />);
    await screen.findByText('Alice Chen');

    await userEvent.click(screen.getByRole('checkbox', { name: /unallocated only/i }));

    expect(screen.getByText('Alice Chen')).toBeInTheDocument();
    expect(screen.queryByText('Bob Smith')).not.toBeInTheDocument();
  });

  it('pencil opens an inline edit form pre-filled with the current percent', async () => {
    mockFetch();
    render(<MappingView />);
    await screen.findByText('Alice Chen');

    const aliceCard = screen.getByText('Alice Chen').closest('[data-card]')!;
    await userEvent.click(within(aliceCard).getByRole('button', { name: /edit AuthService/i }));

    const percentInput = within(aliceCard).getByRole('spinbutton');
    expect((percentInput as HTMLInputElement).value).toBe('60');
  });

  it('saving the edit form PUTs the updated mapping', async () => {
    const fetchSpy = mockFetch();
    render(<MappingView />);
    await screen.findByText('Alice Chen');

    const aliceCard = screen.getByText('Alice Chen').closest('[data-card]')!;
    await userEvent.click(within(aliceCard).getByRole('button', { name: /edit AuthService/i }));

    const percentInput = within(aliceCard).getByRole('spinbutton');
    await userEvent.clear(percentInput);
    await userEvent.type(percentInput, '80');
    await userEvent.click(within(aliceCard).getByRole('button', { name: /^save$/i }));

    const putCall = fetchSpy.mock.calls.find(([url, opts]) =>
      String(url).includes('/api/mappings') && (opts as RequestInit)?.method === 'PUT'
    );
    expect(putCall).toBeTruthy();
    const body = JSON.parse((putCall![1] as RequestInit).body as string);
    expect(body.resource_id).toBe(1);
    expect(body.activity_id).toBe(1);
    expect(body.percent).toBe(80);
  });
});
