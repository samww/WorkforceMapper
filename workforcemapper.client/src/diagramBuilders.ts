import type { DirectAppNode } from './pages/ProductsView';

const CLASS_DEFS = [
  'classDef high fill:#dc2626,color:#fff,stroke:#991b1b',
  'classDef medium fill:#d97706,color:#fff,stroke:#92400e',
  'classDef low fill:#16a34a,color:#fff,stroke:#166534',
];

function riskClass(risk: string): string {
  return risk === 'high' ? 'high' : risk === 'medium' ? 'medium' : 'low';
}

export function buildProductMermaid(directApps: DirectAppNode[], productName: string): string {
  const lines = ['graph TD', ...CLASS_DEFS];
  lines.push(`  prod(("${productName}"))`);
  for (const app of directApps) {
    lines.push(`  prod --> app${app.id}["${app.name}"]:::${riskClass(app.risk)}`);
    for (const dep of app.dependencies)
      lines.push(`  app${app.id} --> app${dep.id}["${dep.name}"]:::${riskClass(dep.risk)}`);
  }
  return lines.join('\n');
}
