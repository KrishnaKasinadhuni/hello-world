import React from 'react';
import { render, screen } from '@testing-library/react';
import App from './App';

test('renders application title', () => {
  render(<App />);
  const titleElement = screen.getByText(/Image Classification & Similarity/i);
  expect(titleElement).toBeInTheDocument();
});
