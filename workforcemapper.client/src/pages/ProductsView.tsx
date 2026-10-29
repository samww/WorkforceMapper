import { useEffect, useState } from 'react';

export interface ContributingApp {
  id: number;
  name: string;
  risk: 'high' | 'medium' | 'low';
  division: string;
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
  division: string;
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

type ProductSortCol = 'name' | 'arr' | 'risk';

const RISK_ORDER: Record<string, number> = { high: 2, medium: 1, low: 0 };

const COL_FIRST_DIR: Record<ProductSortCol, 'asc' | 'desc'> = {
  name: 'asc',
  arr: 'desc',
  risk: 'desc',
};

function sortProducts(products: ProductRisk[], col: ProductSortCol, dir: 'asc' | 'desc'): ProductRisk[] {
  return [...products].sort((a, b) => {
    let cmp = 0;
    if (col === 'name') cmp = a.name.localeCompare(b.name);
    else if (col === 'arr') cmp = a.arr - b.arr;
    else if (col === 'risk') cmp = RISK_ORDER[a.risk] - RISK_ORDER[b.risk];
    const primary = dir === 'asc' ? cmp : -cmp;
    if (primary !== 0) return primary;
    return a.name.localeCompare(b.name);
  });
}

export default function ProductsView({ month, division, onSelectProduct }: Props) {
  const [products, setProducts] = useState<ProductRisk[]>([]);
  const [loadedMonth, setLoadedMonth] = useState<string | null>(null);
  const loading = loadedMonth !== month;
  const [sortCol, setSortCol] = useState<ProductSortCol>('risk');
  const [sortDir, setSortDir] = useState<'asc' | 'desc'>('desc');

  useEffect(() => {
    fetch(`/api/products?month=${month}`)
      .then(r => r.json())
      .then((data: ProductRisk[]) => {
        setProducts(data);
        setLoadedMonth(month);
      });
  }, [month]);

  function handleSort(col: ProductSortCol) {
    if (col === sortCol) {
      setSortDir(d => d === 'asc' ? 'desc' : 'asc');
    } else {
      setSortCol(col);
      setSortDir(COL_FIRST_DIR[col]);
    }
  }

  const filtered = division === 'All'
    ? products
    : products.filter(p => p.contributing_apps.some(a => a.division === division));

  const visible = sortProducts(filtered, sortCol, sortDir);

  function colBtnClass(col: ProductSortCol) {
    return col === sortCol
      ? 'text-gray-100 cursor-pointer'
      : 'text-gray-400 hover:text-gray-200 cursor-pointer';
  }

  function arrow(col: ProductSortCol) {
    if (col !== sortCol) return null;
    return sortDir === 'asc' ? ' ▲' : ' ▼';
  }

  function ariaSortAttr(col: ProductSortCol): 'ascending' | 'descending' | 'none' {
    if (col !== sortCol) return 'none';
    return sortDir === 'asc' ? 'ascending' : 'descending';
  }

  if (loading) return (
    <div className="flex justify-center items-center h-40">
      <div role="status" className="w-8 h-8 rounded-full border-2 border-gray-600 border-t-blue-400 animate-spin" />
    </div>
  );

  return (
    <div className="p-4">
      <div className="flex items-center justify-between mb-4">
        <h2 className="text-xl font-semibold text-gray-100">Products</h2>
      </div>
      <table className="w-full text-sm text-gray-200">
        <thead>
          <tr className="text-left border-b border-gray-700">
            <th aria-sort={ariaSortAttr('name')} className="pb-2 pr-4"><button className={colBtnClass('name')} onClick={() => handleSort('name')}>Name{arrow('name')}</button></th>
            <th aria-sort={ariaSortAttr('arr')} className="pb-2 pr-4"><button className={colBtnClass('arr')} onClick={() => handleSort('arr')}>ARR{arrow('arr')}</button></th>
            <th aria-sort={ariaSortAttr('risk')} className="pb-2"><button className={colBtnClass('risk')} onClick={() => handleSort('risk')}>Risk{arrow('risk')}</button></th>
          </tr>
        </thead>
        <tbody>
          {visible.map(product => (
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
