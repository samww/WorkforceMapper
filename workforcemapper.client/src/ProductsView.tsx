import { useEffect, useState } from 'react';

export interface ContributingApp {
  id: number;
  name: string;
  risk: 'high' | 'medium' | 'low';
}

export interface DirectAppNode {
  id: number;
  name: string;
  risk: 'high' | 'medium' | 'low';
  dependencies: ContributingApp[];
}

export interface ProductRisk {
  id: number;
  name: string;
  arr: number;
  risk: 'high' | 'medium' | 'low';
  contributing_apps: ContributingApp[];
  direct_apps: DirectAppNode[];
}

interface Props {
  month: string;
  onSelectProduct: (product: ProductRisk) => void;
}

const BADGE: Record<string, string> = {
  high: 'bg-red-600 text-white',
  medium: 'bg-amber-500 text-white',
  low: 'bg-green-600 text-white',
};

function formatArr(usd: number) {
  return `$${Math.round(usd / 1000).toLocaleString()}k`;
}

export default function ProductsView({ month, onSelectProduct }: Props) {
  const [products, setProducts] = useState<ProductRisk[]>([]);

  useEffect(() => {
    fetch(`/api/products?month=${month}`)
      .then(r => r.json())
      .then((data: ProductRisk[]) => {
        const riskOrder = { high: 0, medium: 1, low: 2 };
        data.sort((a, b) => {
          const rd = riskOrder[a.risk] - riskOrder[b.risk];
          return rd !== 0 ? rd : b.arr - a.arr;
        });
        setProducts(data);
      });
  }, [month]);

  return (
    <div className="p-4">
      <h2 className="text-xl font-semibold text-gray-100 mb-4">Products</h2>
      <table className="w-full text-sm text-gray-200">
        <thead>
          <tr className="text-left text-gray-400 border-b border-gray-700">
            <th className="pb-2 pr-4">Name</th>
            <th className="pb-2 pr-4">ARR</th>
            <th className="pb-2">Risk</th>
          </tr>
        </thead>
        <tbody>
          {products.map(product => (
            <tr
              key={product.id}
              className="border-b border-gray-800 hover:bg-gray-800 cursor-pointer"
              onClick={() => onSelectProduct(product)}
            >
              <td className="py-2 pr-4">{product.name}</td>
              <td className="py-2 pr-4 text-gray-400">{formatArr(product.arr)}</td>
              <td className="py-2">
                <span className={`px-2 py-0.5 rounded text-xs font-medium ${BADGE[product.risk]}`}>
                  {product.risk}
                </span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
