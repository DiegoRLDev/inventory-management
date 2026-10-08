import { Component, OnInit, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatTableModule } from '@angular/material/table';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { InvoiceItem, Product } from '../models';
import { ProductService } from '../product.service';
import { InvoiceService } from '../invoice.service';
import { apiErrorMessage } from '../api-error';

@Component({
  selector: 'app-invoice-create-page',
  imports: [
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatTableModule,
    MatSnackBarModule
  ],
  templateUrl: './invoice-create-page.component.html',
  styleUrl: './invoice-create-page.component.css'
})
export class InvoiceCreatePageComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly productsApi = inject(ProductService);
  private readonly invoicesApi = inject(InvoiceService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly router = inject(Router);

  products: Product[] = [];
  items: InvoiceItem[] = [];
  columns = ['productCode', 'productDescription', 'quantity', 'actions'];
  saving = false;

  readonly itemForm = this.fb.nonNullable.group({
    productId: ['', Validators.required],
    quantity: [1, [Validators.required, Validators.min(1)]]
  });

  ngOnInit(): void {
    this.productsApi.list().subscribe({
      next: products => (this.products = products),
      error: error => this.snackBar.open(apiErrorMessage(error), 'Fechar', { duration: 4000 })
    });
  }

  addItem(): void {
    if (this.itemForm.invalid) {
      this.itemForm.markAllAsTouched();
      return;
    }

    const { productId, quantity } = this.itemForm.getRawValue();
    const product = this.products.find(item => item.id === productId);
    if (!product) {
      return;
    }

    this.items = [
      ...this.items,
      {
        productId: product.id,
        productCode: product.code,
        productDescription: product.description,
        quantity
      }
    ];
    this.itemForm.patchValue({ quantity: 1 });
  }

  removeItem(index: number): void {
    this.items = this.items.filter((_, itemIndex) => itemIndex !== index);
  }

  save(): void {
    if (this.items.length === 0) {
      this.snackBar.open('Adicione pelo menos um produto.', 'Fechar', { duration: 3000 });
      return;
    }

    this.saving = true;
    this.invoicesApi.create(this.items).subscribe({
      next: invoice => {
        this.saving = false;
        this.items = [];
        this.itemForm.reset({ productId: '', quantity: 1 });
        this.snackBar.open(`Nota ${invoice.number} criada.`, 'Fechar', { duration: 3000 });
        void this.router.navigate(['/notas']);
      },
      error: error => {
        this.saving = false;
        this.snackBar.open(apiErrorMessage(error), 'Fechar', { duration: 5000 });
      }
    });
  }
}
