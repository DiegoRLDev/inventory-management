import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Invoice, InvoiceItem } from './models';

@Injectable({ providedIn: 'root' })
export class InvoiceService {
  private readonly baseUrl = 'http://localhost:5082/api/invoices';

  constructor(private readonly http: HttpClient) {}

  list() {
    return this.http.get<Invoice[]>(this.baseUrl);
  }

  create(items: InvoiceItem[]) {
    return this.http.post<Invoice>(this.baseUrl, { items });
  }

  issue(id: string) {
    return this.http.post<Invoice>(`${this.baseUrl}/${id}/issue`, {});
  }
}
