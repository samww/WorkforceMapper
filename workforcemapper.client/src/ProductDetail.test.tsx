import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import ProductDetail from './ProductDetail';

const PRODUCT = {
  id: 1,
  name: 'DataLicensing',
  arr: 6200000,
  risk: 'high' as const,
  contributing_apps: [
    { id: 4, name: 'AuthService', risk: 'high' as const },
    { id: 1, name: 'CoreDataWarehouse', risk: 'low' as const },
  ],
  direct_apps: [
    {
      id: 1, name: 'CoreDataWarehouse', risk: 'low' as const,
      dependencies: [{ id: 4, name: 'AuthService', risk: 'high' as const }],
    },
    {
      id: 2, name: 'APIGateway', risk: 'low' as const,
      dependencies: [{ id: 4, name: 'AuthService', risk: 'high' as const }],
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

  it('shows direct apps in the dependency tree', () => {
    render(<ProductDetail product={PRODUCT} onBack={() => {}} />);
    expect(screen.getByText('CoreDataWarehouse')).toBeInTheDocument();
    expect(screen.getByText('APIGateway')).toBeInTheDocument();
  });

  it('shows nested app dependencies under a direct app', () => {
    render(<ProductDetail product={PRODUCT} onBack={() => {}} />);
    // AuthService appears as a dependency of both direct apps; at least one instance must be visible
    expect(screen.getAllByText('AuthService').length).toBeGreaterThan(0);
  });
});
