import { Component, OnInit, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { Product } from '../models';
import { ProductService } from '../product.service';
import { apiErrorMessage } from '../api-error';

@Component({
  selector: 'app-products-page',
  imports: [
    ReactiveFormsModule,
    MatTableModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatSnackBarModule
  ],
  templateUrl: './products-page.component.html',
  styleUrl: './products-page.component.css'
})
export class ProductsPageComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly productsApi = inject(ProductService);
  private readonly snackBar = inject(MatSnackBar);

  products: Product[] = [];
  columns = ['code', 'description', 'balance'];
  saving = false;

  readonly form = this.fb.nonNullable.group({
    code: ['', Validators.required],
    description: ['', Validators.required],
    balance: [0, [Validators.required, Validators.min(0)]]
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.productsApi.list().subscribe({
      next: products => (this.products = products),
      error: error => this.snackBar.open(apiErrorMessage(error), 'Fechar', { duration: 4000 })
    });
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving = true;
    this.productsApi.create(this.form.getRawValue()).subscribe({
      next: () => {
        this.saving = false;
        this.form.reset({ code: '', description: '', balance: 0 });
        this.snackBar.open('Produto cadastrado.', 'Fechar', { duration: 3000 });
        this.load();
      },
      error: error => {
        this.saving = false;
        this.snackBar.open(apiErrorMessage(error), 'Fechar', { duration: 5000 });
      }
    });
  }
}
