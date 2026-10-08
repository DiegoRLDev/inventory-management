export interface Product {
  id: string;
  code: string;
  description: string;
  balance: number;
}

export interface InvoiceItem {
  productId: string;
  productCode: string;
  productDescription: string;
  quantity: number;
}

export interface Invoice {
  id: string;
  number: number;
  status: 'Open' | 'Closed';
  createdAt: string;
  items: InvoiceItem[];
}
