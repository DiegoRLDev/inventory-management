import { Component, OnInit, inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { Invoice } from '../models';
import { InvoiceService } from '../invoice.service';
import { apiErrorMessage } from '../api-error';

@Component({
  selector: 'app-invoices-page',
  imports: [
    DatePipe,
    MatTableModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    MatSnackBarModule
  ],
  templateUrl: './invoices-page.component.html',
  styleUrl: './invoices-page.component.css'
})
export class InvoicesPageComponent implements OnInit {
  private readonly invoicesApi = inject(InvoiceService);
  private readonly snackBar = inject(MatSnackBar);

  invoices: Invoice[] = [];
  columns = ['number', 'status', 'createdAt', 'items', 'actions'];
  issuingId: string | null = null;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.invoicesApi.list().subscribe({
      next: invoices => (this.invoices = invoices),
      error: error => this.snackBar.open(apiErrorMessage(error), 'Fechar', { duration: 4000 })
    });
  }

  issue(invoice: Invoice): void {
    this.issuingId = invoice.id;
    this.invoicesApi.issue(invoice.id).subscribe({
      next: issued => {
        this.issuingId = null;
        this.invoices = this.invoices.map(item => item.id === issued.id ? issued : item);
        this.snackBar.open(`Nota ${issued.number} emitida.`, 'Imprimir', { duration: 5000 })
          .onAction()
          .subscribe(() => this.print(issued));
      },
      error: error => {
        this.issuingId = null;
        this.snackBar.open(apiErrorMessage(error), 'Fechar', { duration: 6000 });
        this.load();
      }
    });
  }

  print(invoice: Invoice): void {
    const items = invoice.items
      .map(item => `<tr><td>${item.productCode}</td><td>${item.productDescription}</td><td>${item.quantity}</td></tr>`)
      .join('');
    const popup = window.open('', '_blank', 'width=720,height=800');
    if (!popup) {
      this.snackBar.open('Permita pop-ups para imprimir a nota.', 'Fechar', { duration: 4000 });
      return;
    }

    popup.document.write(`
      <html>
        <head>
          <title>Nota ${invoice.number}</title>
          <style>
            body { font-family: Arial, sans-serif; padding: 24px; }
            table { width: 100%; border-collapse: collapse; margin-top: 16px; }
            th, td { border: 1px solid #ccc; padding: 8px; text-align: left; }
          </style>
        </head>
        <body>
          <h1>Nota Fiscal ${invoice.number}</h1>
          <p>Status: ${invoice.status}</p>
          <p>Emissão: ${new Date(invoice.createdAt).toLocaleString()}</p>
          <table>
            <thead><tr><th>Código</th><th>Descrição</th><th>Qtd</th></tr></thead>
            <tbody>${items}</tbody>
          </table>
        </body>
      </html>
    `);
    popup.document.close();
    popup.focus();
    popup.print();
  }
}
