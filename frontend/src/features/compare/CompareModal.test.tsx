import { useEffect } from 'react'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { MemoryRouter, Routes, Route } from 'react-router-dom'
import { branchApi, type BranchDto } from '../../api/branchApi'
import { catalogApi, type ProductDetailDto } from '../../api/catalogApi'
import { CompareProvider, useCompare, type CompareProduct } from './CompareContext'
import { CompareModal } from './CompareModal'

const branches: BranchDto[] = [
  {
    id: 'branch-1',
    name: 'AptechMart Quận 1',
    address: '123 Nguyễn Huệ',
    phone: null,
    latitude: null,
    longitude: null,
    isActive: true,
  },
  {
    id: 'branch-2',
    name: 'AptechMart Quận 3',
    address: '456 Đường 3 Tháng 2',
    phone: null,
    latitude: null,
    longitude: null,
    isActive: true,
  },
]

const phoneA: CompareProduct = {
  id: 'prod-a',
  categoryId: 'cat-1',
  categoryName: 'Điện thoại & Tablet',
  categorySlug: 'dien-thoai',
}
const phoneB: CompareProduct = {
  id: 'prod-b',
  categoryId: 'cat-1',
  categoryName: 'Điện thoại & Tablet',
  categorySlug: 'dien-thoai',
}

const detailNoBranch: ProductDetailDto = {
  id: 'prod-a',
  name: 'iPhone 15 128GB',
  slug: 'iphone-15',
  sku: 'SKU-A',
  description: 'Màn hình Super Retina XDR.',
  basePrice: 22990000,
  unit: 'cái',
  imageUrl: null,
  categoryId: 'cat-1',
  categoryName: 'Điện thoại & Tablet',
  categorySlug: 'dien-thoai',
  brandId: 'brand-1',
  brandName: 'Apple',
  branchInventory: null,
}

const detailBranch1: ProductDetailDto = {
  ...detailNoBranch,
  branchInventory: {
    branchId: 'branch-1',
    sellingPrice: 21990000,
    availableQuantity: 7,
    onHand: 9,
  },
}

const detailBranch2: ProductDetailDto = {
  ...detailNoBranch,
  branchInventory: {
    branchId: 'branch-2',
    sellingPrice: 20990000,
    availableQuantity: 3,
    onHand: 5,
  },
}

function Harness({ products }: { products: CompareProduct[] }) {
  const { addToCompare, openModal, closeModal } = useCompare()
  useEffect(() => {
    products.forEach((p) => addToCompare(p))
    openModal()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])
  return (
    <div>
      <button onClick={closeModal}>dialog-close</button>
      <button onClick={openModal}>dialog-open</button>
    </div>
  )
}

function renderCompare(products: CompareProduct[] = [phoneA]) {
  return render(
    <MemoryRouter initialEntries={['/']}>
      <Routes>
        <Route path="/" element={<div>Browse page</div>} />
        <Route path="/product/:id" element={<div data-testid="detail-route">Detail page</div>} />
      </Routes>
      <CompareProvider>
        <Harness products={products} />
        <CompareModal />
      </CompareProvider>
    </MemoryRouter>
  )
}

describe('CompareModal', () => {
  beforeEach(() => {
    localStorage.clear()
    vi.spyOn(branchApi, 'getBranches').mockResolvedValue(branches)
    vi.spyOn(catalogApi, 'getProductById').mockResolvedValue(detailNoBranch)
  })

  afterEach(() => {
    localStorage.clear()
    vi.restoreAllMocks()
  })

  it('loads and lists active branches in the selector', async () => {
    renderCompare()
    const select = await screen.findByLabelText('Kho hàng:')
    expect(screen.getByRole('option', { name: 'Tất cả chi nhánh' })).toBeInTheDocument()
    expect(screen.getByRole('option', { name: 'AptechMart Quận 1' })).toBeInTheDocument()
    expect(select).toBeEnabled()
    expect(branchApi.getBranches).toHaveBeenCalled()
  })

  it('allows retrying when the branch list fails to load', async () => {
    vi.restoreAllMocks()
    vi.spyOn(branchApi, 'getBranches')
      .mockRejectedValueOnce(new Error('network'))
      .mockResolvedValueOnce(branches)
    vi.spyOn(catalogApi, 'getProductById').mockResolvedValue(detailNoBranch)
    renderCompare()
    const retry = await screen.findByRole('button', { name: 'Tải lại danh sách kho' })
    expect(screen.getByLabelText('Kho hàng:')).toBeDisabled()
    fireEvent.click(retry)
    expect(await screen.findByRole('option', { name: 'AptechMart Quận 1' })).toBeInTheDocument()
    expect(branchApi.getBranches).toHaveBeenCalledTimes(2)
  })

  it('fetches product details and renders them', async () => {
    renderCompare()
    expect(await screen.findByText('iPhone 15 128GB')).toBeInTheDocument()
    expect(screen.getByText('Màn hình Super Retina XDR.')).toBeInTheDocument()
    expect(catalogApi.getProductById).toHaveBeenCalledWith(
      'prod-a',
      undefined,
      expect.any(AbortSignal)
    )
  })

  it('refetches product details when the branch changes', async () => {
    const getProductSpy = vi.spyOn(catalogApi, 'getProductById')
      .mockResolvedValueOnce(detailNoBranch)
      .mockResolvedValueOnce(detailBranch1)
    renderCompare()
    await screen.findByText('iPhone 15 128GB')

    fireEvent.change(await screen.findByLabelText('Kho hàng:'), {
      target: { value: 'branch-1' },
    })

    await waitFor(() => {
      expect(getProductSpy).toHaveBeenLastCalledWith('prod-a', 'branch-1', expect.any(AbortSignal))
    })
    expect(await screen.findByText('Còn 7 sản phẩm')).toBeInTheDocument()
  })

  it('keeps loaded data when the modal is closed and reopened', async () => {
    const getProductSpy = vi
      .spyOn(catalogApi, 'getProductById')
      .mockResolvedValue(detailNoBranch)
    renderCompare()
    await screen.findByText('iPhone 15 128GB')
    expect(getProductSpy).toHaveBeenCalledTimes(1)

    fireEvent.click(screen.getByRole('button', { name: 'dialog-close' }))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'dialog-open' }))
    expect(await screen.findByText('iPhone 15 128GB')).toBeInTheDocument()
    expect(getProductSpy).toHaveBeenCalledTimes(1)
  })

  it('ignores a stale response from a previously selected branch', async () => {
    let resolveBranch1: (value: ProductDetailDto) => void
    const getProductSpy = vi
      .spyOn(catalogApi, 'getProductById')
      .mockImplementation((_id, branchId, signal) => {
        if (branchId === 'branch-1') {
          return new Promise((resolve, reject) => {
            resolveBranch1 = resolve
            ;(signal as AbortSignal | undefined)?.addEventListener('abort', () =>
              reject(new DOMException('Aborted', 'AbortError'))
            )
          })
        }
        return branchId === 'branch-2'
          ? Promise.resolve(detailBranch2)
          : Promise.resolve(detailNoBranch)
      })
    renderCompare()
    await screen.findByText('iPhone 15 128GB')

    fireEvent.change(await screen.findByLabelText('Kho hàng:'), {
      target: { value: 'branch-1' },
    })
    await waitFor(() => {
      expect(getProductSpy).toHaveBeenLastCalledWith('prod-a', 'branch-1', expect.any(AbortSignal))
    })

    fireEvent.change(screen.getByLabelText('Kho hàng:'), {
      target: { value: 'branch-2' },
    })
    expect(await screen.findByText('Còn 3 sản phẩm')).toBeInTheDocument()

    resolveBranch1!(detailBranch1)
    await waitFor(() => {
      expect(screen.getByText('Còn 3 sản phẩm')).toBeInTheDocument()
    })
    expect(screen.queryByText('Còn 7 sản phẩm')).not.toBeInTheDocument()
  })

  it('navigates to the product detail page and closes the modal via Mua ngay', async () => {
    renderCompare()
    await screen.findByText('iPhone 15 128GB')
    const buyNow = screen.getByRole('link', { name: 'Mua ngay' })
    expect(buyNow).toHaveAttribute('href', '/product/prod-a')
    fireEvent.click(buyNow)
    expect(screen.getByTestId('detail-route')).toBeInTheDocument()
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('closes the modal when navigating via the product name link', async () => {
    renderCompare()
    const nameLink = await screen.findByRole('link', { name: 'iPhone 15 128GB' })
    fireEvent.click(nameLink)
    expect(screen.getByTestId('detail-route')).toBeInTheDocument()
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('removes a product from the comparison table', async () => {
    renderCompare([phoneA, phoneB])
    const removeButtons = await screen.findAllByRole('button', {
      name: /Xóa .* khỏi danh sách so sánh/,
    })
    expect(removeButtons).toHaveLength(2)
    fireEvent.click(removeButtons[0])
    expect(await screen.findByText('Chọn sản phẩm thứ 2')).toBeInTheDocument()
  })

  it('shows an empty state when no products are compared', async () => {
    renderCompare([])
    expect(await screen.findByText('Chưa có sản phẩm nào để so sánh.')).toBeInTheDocument()
  })
})