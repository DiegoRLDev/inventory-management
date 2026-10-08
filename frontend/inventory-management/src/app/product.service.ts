import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Product } from './models';

@Injectable({ providedIn: 'root' })
export class ProductService {
  private readonly baseUrl = 'http://localhost:5081/api/products';

  constructor(private readonly http: HttpClient) {}

  list() {
    return this.http.get<Product[]>(this.baseUrl);
  }

  create(product: { code: string; description: string; balance: number }) {
    return this.http.post<Product>(this.baseUrl, product);
  }
}
