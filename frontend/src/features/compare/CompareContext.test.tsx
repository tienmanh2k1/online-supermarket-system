import { act, render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { CompareProvider, useCompare, type CompareProduct } from './CompareContext'

const phoneA: CompareProduct = {
  id: 'phone-a',
  categoryId: 'phone-leaf',
  categoryName: 'Điện thoại',
  categorySlug: 'dien-thoai',
}
const phoneB: CompareProduct = {
  id: 'phone-b',
  categoryId: 'phone-leaf',
  categoryName: 'Điện thoại',
  categorySlug: 'dien-thoai',
}
const phoneC: CompareProduct = {
  id: 'phone-c',
  categoryId: 'phone-leaf',
  categoryName: 'Điện thoại',
  categorySlug: 'dien-thoai',
}
const phoneD: CompareProduct = {
  id: 'phone-d',
  categoryId: 'phone-leaf',
  categoryName: 'Điện thoại',
  categorySlug: 'dien-thoai',
}
const phoneE: CompareProduct = {
  id: 'phone-e',
  categoryId: 'phone-leaf',
  categoryName: 'Điện thoại',
  categorySlug: 'dien-thoai',
}
const tablet: CompareProduct = {
  id: 'tablet-a',
  categoryId: 'tablet-leaf',
  categoryName: 'Máy tính bảng',
  categorySlug: 'may-tinh-bang',
}
const unknown: CompareProduct = {
  id: 'unknown-a',
  categoryId: 'uncategorized-id',
  categoryName: 'Chưa phân loại',
  categorySlug: 'uncategorized',
}

function Harness() {
  const ctx = useCompare()
  return (
    <div>
      <span data-testid="count">{ctx.compareProducts.length}</span>
      <span data-testid="canAddMore">{String(ctx.canAddMore)}</span>
      <span data-testid="hasProduct">{String(ctx.hasProduct)}</span>
      <span data-testid="modalOpen">{String(ctx.isModalOpen)}</span>
      <button onClick={() => ctx.addToCompare(phoneA)}>add-phone-a</button>
      <button onClick={() => ctx.addToCompare(phoneB)}>add-phone-b</button>
      <button onClick={() => ctx.addToCompare(phoneC)}>add-phone-c</button>
      <button onClick={() => ctx.addToCompare(phoneD)}>add-phone-d</button>
      <button onClick={() => ctx.addToCompare(phoneE)}>add-phone-e</button>
      <button onClick={() => ctx.addToCompare(tablet)}>add-tablet</button>
      <button onClick={() => ctx.addToCompare(unknown)}>add-unknown</button>
      <button onClick={() => ctx.removeFromCompare(phoneA.id)}>remove-phone-a</button>
      <button onClick={() => ctx.clearCompare()}>clear</button>
      <button onClick={ctx.openModal}>open</button>
      <button onClick={ctx.closeModal}>close</button>
      <span data-testid="warning">{ctx.getDifferentCategoryWarning(tablet)}</span>
      <span data-testid="unknown-warning">{ctx.getDifferentCategoryWarning(unknown)}</span>
    </div>
  )
}

function renderHarness() {
  return render(
    <CompareProvider>
      <Harness />
    </CompareProvider>
  )
}

describe('CompareContext', () => {
  beforeEach(() => {
    localStorage.clear()
    vi.restoreAllMocks()
  })

  it('allows adding up to 4 products of the same leaf category', () => {
    renderHarness()
    act(() => {
      screen.getByRole('button', { name: 'add-phone-a' }).click()
      screen.getByRole('button', { name: 'add-phone-b' }).click()
      screen.getByRole('button', { name: 'add-phone-c' }).click()
      screen.getByRole('button', { name: 'add-phone-d' }).click()
    })
    expect(screen.getByTestId('count')).toHaveTextContent('4')
    expect(screen.getByTestId('canAddMore')).toHaveTextContent('false')
    expect(screen.getByTestId('hasProduct')).toHaveTextContent('true')
  })

  it('supports adding 3 products fulfilling FR-104 requirement', () => {
    renderHarness()
    act(() => {
      screen.getByRole('button', { name: 'add-phone-a' }).click()
      screen.getByRole('button', { name: 'add-phone-b' }).click()
      screen.getByRole('button', { name: 'add-phone-c' }).click()
    })
    expect(screen.getByTestId('count')).toHaveTextContent('3')
    expect(screen.getByTestId('canAddMore')).toHaveTextContent('true')
    expect(screen.getByTestId('hasProduct')).toHaveTextContent('true')
  })

  it('rejects a fifth product when the list is full', () => {
    renderHarness()
    act(() => {
      screen.getByRole('button', { name: 'add-phone-a' }).click()
      screen.getByRole('button', { name: 'add-phone-b' }).click()
      screen.getByRole('button', { name: 'add-phone-c' }).click()
      screen.getByRole('button', { name: 'add-phone-d' }).click()
    })
    act(() => {
      screen.getByRole('button', { name: 'add-phone-e' }).click()
    })
    expect(screen.getByTestId('count')).toHaveTextContent('4')
  })

  it('rejects products from different leaf categories', () => {
    renderHarness()
    act(() => {
      screen.getByRole('button', { name: 'add-phone-a' }).click()
    })
    act(() => {
      screen.getByRole('button', { name: 'add-tablet' }).click()
    })
    expect(screen.getByTestId('count')).toHaveTextContent('1')
    expect(screen.getByTestId('warning')).toHaveTextContent(
      'Chỉ có thể so sánh các sản phẩm cùng loại.',
    )
  })

  it('rejects an uncategorized product even when compare list is empty', () => {
    renderHarness()
    act(() => {
      screen.getByRole('button', { name: 'add-unknown' }).click()
    })
    expect(screen.getByTestId('count')).toHaveTextContent('0')
    expect(screen.getByTestId('unknown-warning')).toHaveTextContent(
      'Sản phẩm chưa được phân loại nên chưa thể so sánh.',
    )
  })

  it('keeps the category invariant across adds in a single batch', () => {
    renderHarness()
    act(() => {
      screen.getByRole('button', { name: 'add-phone-a' }).click()
      screen.getByRole('button', { name: 'add-tablet' }).click()
    })
    expect(screen.getByTestId('count')).toHaveTextContent('1')
    expect(screen.getByTestId('warning')).toHaveTextContent(
      'Chỉ có thể so sánh các sản phẩm cùng loại.',
    )
  })

  it('never exceeds four products across adds in a single batch', () => {
    renderHarness()
    act(() => {
      screen.getByRole('button', { name: 'add-phone-a' }).click()
      screen.getByRole('button', { name: 'add-phone-b' }).click()
      screen.getByRole('button', { name: 'add-phone-c' }).click()
      screen.getByRole('button', { name: 'add-phone-d' }).click()
      screen.getByRole('button', { name: 'add-phone-e' }).click()
    })
    expect(screen.getByTestId('count')).toHaveTextContent('4')
    expect(screen.getByTestId('canAddMore')).toHaveTextContent('false')
  })

  it('removes a single product', () => {
    renderHarness()
    act(() => {
      screen.getByRole('button', { name: 'add-phone-a' }).click()
      screen.getByRole('button', { name: 'add-phone-b' }).click()
      screen.getByRole('button', { name: 'remove-phone-a' }).click()
    })
    expect(screen.getByTestId('count')).toHaveTextContent('1')
    expect(screen.getByTestId('canAddMore')).toHaveTextContent('true')
  })

  it('clears all products', () => {
    renderHarness()
    act(() => {
      screen.getByRole('button', { name: 'add-phone-a' }).click()
      screen.getByRole('button', { name: 'add-phone-b' }).click()
      screen.getByRole('button', { name: 'clear' }).click()
    })
    expect(screen.getByTestId('count')).toHaveTextContent('0')
    expect(screen.getByTestId('hasProduct')).toHaveTextContent('false')
  })

  it('toggles the modal via openModal and closeModal', () => {
    renderHarness()
    act(() => {
      screen.getByRole('button', { name: 'open' }).click()
    })
    expect(screen.getByTestId('modalOpen')).toHaveTextContent('true')
    act(() => {
      screen.getByRole('button', { name: 'close' }).click()
    })
    expect(screen.getByTestId('modalOpen')).toHaveTextContent('false')
  })

  it('opens the modal on the global open-compare-modal event', () => {
    renderHarness()
    act(() => {
      window.dispatchEvent(new Event('open-compare-modal'))
    })
    expect(screen.getByTestId('modalOpen')).toHaveTextContent('true')
  })
})