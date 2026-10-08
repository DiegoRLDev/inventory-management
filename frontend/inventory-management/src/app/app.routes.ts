import { Routes } from '@angular/router';
import { ProductsPageComponent } from './pages/products-page.component';
import { InvoiceCreatePageComponent } from './pages/invoice-create-page.component';
import { InvoicesPageComponent } from './pages/invoices-page.component';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'produtos' },
  { path: 'produtos', component: ProductsPageComponent },
  { path: 'notas/nova', component: InvoiceCreatePageComponent },
  { path: 'notas', component: InvoicesPageComponent }
];
