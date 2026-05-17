import { describe, it, expect } from 'vitest';
import { buildProductMermaid } from '../diagramBuilders';
import type { DirectAppNode } from '../pages/ProductsView';

describe('buildProductMermaid', () => {
  it('returns header, classDefs, and product circle node', () => {
    const result = buildProductMermaid([], 'DataLicensing');
    expect(result).toContain('graph TD');
    expect(result).toContain('classDef high');
    expect(result).toContain('classDef medium');
    expect(result).toContain('classDef low');
    expect(result).toContain('prod(("DataLicensing"))');
  });

  it('connects product circle to direct app rectangles', () => {
    const apps: DirectAppNode[] = [
      { id: 5, name: 'AuthService', risk: 'high', dependencies: [] },
    ];
    const result = buildProductMermaid(apps, 'DataLicensing');
    expect(result).toContain('prod --> app5["AuthService"]:::high');
  });

  it('includes dependency edge between apps', () => {
    const apps: DirectAppNode[] = [
      {
        id: 1,
        name: 'Portal',
        risk: 'low',
        dependencies: [{ id: 5, name: 'AuthService', risk: 'high', division: 'Platform Engineering' }],
      },
    ];
    const result = buildProductMermaid(apps, 'DataLicensing');
    expect(result).toContain('app1 --> app5["AuthService"]:::high');
  });
});
