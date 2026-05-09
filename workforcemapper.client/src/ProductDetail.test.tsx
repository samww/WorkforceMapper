import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import ProductDetail from './ProductDetail';

vi.mock('mermaid', () => ({
  default: {
    initialize: vi.fn(),
    render: vi.fn().mockResolvedValue({ svg: '<svg />' }),
  },
}));

const PRODUCT = {
  id: 1,
  name: 'DataLicensing',
  arr: 6200000,
  risk: 'high' as const,
  contributing_apps: [
    { id: 4, name: 'AuthService', risk: 'high' as const, division: 'Platform Engineering' },
    { id: 1, name: 'CoreDataWarehouse', risk: 'low' as const, division: 'Platform Engineering' },
  ],
  direct_apps: [
    {
      id: 1, name: 'CoreDataWarehouse', risk: 'low' as const,
      dependencies: [{ id: 4, name: 'AuthService', risk: 'high' as const, division: 'Platform Engineering' }],
    },
    {
      id: 2, name: 'APIGateway', risk: 'low' as const,
      dependencies: [{ id: 4, name: 'AuthService', risk: 'high' as const, division: 'Platform Engineering' }],
    },
  ],
};

describe('ProductDetail', () => {
  it('renders without crashing and shows product name', () => {
    render(<ProductDetail product={PRODUCT} onBack={() => {}} />);
    expect(screen.getByText('DataLicensing')).toBeInTheDocument();
  });

  it('shows the product risk badge', () => {
    render(<ProductDetail product={PRODUCT} onBack={() => {}} />);
    const badges = screen.getAllByText(/high/i);
    expect(badges.length).toBeGreaterThan(0);
  });

  it('shows the dependency tree heading when direct_apps is non-empty', () => {
    render(<ProductDetail product={PRODUCT} onBack={() => {}} />);
    expect(screen.getByText('Dependency Tree')).toBeInTheDocument();
  });

  it('renders the mermaid diagram container', () => {
    const { container } = render(<ProductDetail product={PRODUCT} onBack={() => {}} />);
    expect(container.querySelector('.overflow-x-auto')).toBeInTheDocument();
  });

  it('omits the dependency tree section when direct_apps is empty', () => {
    const noApps = { ...PRODUCT, direct_apps: [] };
    render(<ProductDetail product={noApps} onBack={() => {}} />);
    expect(screen.queryByText('Dependency Tree')).not.toBeInTheDocument();
  });
});
