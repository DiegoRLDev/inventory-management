import { HttpErrorResponse } from '@angular/common/http';

export function apiErrorMessage(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    const body = error.error as { error?: string } | null;
    return body?.error ?? error.message;
  }

  return 'Erro inesperado.';
}
