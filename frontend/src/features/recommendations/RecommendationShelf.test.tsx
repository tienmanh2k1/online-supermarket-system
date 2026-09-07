import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { MemoryRouter } from 'react-router-dom'
import { recommendationApi, type RecommendationItemDto } from '../../api/recommendationApi'
import { RecommendationShelf } from './RecommendationShelf'
import { RecommendationShelfLoader } from './RecommendationShelfLoader'

const mockItems: RecommendationItemDto[] = [
  {
    productId: 'prod-1',
    name: 'Cà phê Bột Phúc Long',
    slug: 'ca-phe-bot-phuc-long',
    imageUrl: 'https://example.com/coffee.jpg',
    price: 99000,
    availableQuantity: 4,
    score: 0.9,
    reason: 'Được nhiều người quan tâm',
  },
  {
    productId: 'prod-2',
    name: 'Sữa tươi tiệt trùng',
    slug: 'sua-tuoi-tiet-trung',
    imageUrl: null,
    price: 38000,
    availableQuantity: null,
    score: 0.8,
    reason: 'Được nhiều người quan tâm',
  },
]

describe('Recommendation Shelf Feature', () => {
  afterEach(() => vi.restoreAllMocks())

  describe('RecommendationShelf', () => {
    it('renders cards with title, price and reason', () => {
      render(
        <MemoryRouter>
          <RecommendationShelf
            title="Gợi ý dành cho bạn"
            contextLabel="Vì sao"
            items={mockItems}
            loading={false}
            isError={false}
          />
        </MemoryRouter>,
      )

      expect(screen.getByText('Gợi ý dành cho bạn')).toBeInTheDocument()
      expect(screen.getByText('Cà phê Bột Phúc Long')).toBeInTheDocument()
      expect(screen.getByText(/99\.000/)).toBeInTheDocument()
      expect(screen.getByText('Còn 4 tại kho này')).toBeInTheDocument()
    })

    it('renders loading skeletons while fetching', () => {
      render(
        <RecommendationShelf
          title="Gợi ý dành cho bạn"
          contextLabel="Vì sao"
          items={[]}
          loading
          isError={false}
        />,
      )

      expect(screen.getByTestId('recommendation-shelf-loading')).toBeInTheDocument()
    })

    it('shows error state with retry button that re-triggers', () => {
      const retry = vi.fn()
      render(
        <RecommendationShelf
          title="Gợi ý dành cho bạn"
          contextLabel="Vì sao"
          items={[]}
          loading={false}
          isError
          onRetry={retry}
        />,
      )

      fireEvent.click(screen.getByText('Thử lại'))
      expect(retry).toHaveBeenCalledTimes(1)
    })

    it('renders nothing when items are empty after load', () => {
      const { container } = render(
        <RecommendationShelf
          title="Gợi ý dành cho bạn"
          contextLabel="Vì sao"
          items={[]}
          loading={false}
          isError={false}
        />,
      )

      expect(container.firstChild).toBeNull()
    })
  })

  describe('RecommendationShelfLoader', () => {
    it('fetches homepage recommendations and passes branchId', async () => {
      const getRecs = vi
        .spyOn(recommendationApi, 'getRecommendations')
        .mockResolvedValue({ sourceScope: 'Global', items: mockItems })

      render(
        <MemoryRouter>
          <RecommendationShelfLoader branchId="branch-1" />
        </MemoryRouter>,
      )

      await waitFor(() => {
        expect(screen.getByText('Cà phê Bột Phúc Long')).toBeInTheDocument()
      })
      expect(getRecs).toHaveBeenCalledWith(
        expect.objectContaining({ branchId: 'branch-1', limit: 8 }),
      )
    })

    it('fetches similar products for product detail page', async () => {
      const getSimilar = vi
        .spyOn(recommendationApi, 'getProductRecommendations')
        .mockResolvedValue({ sourceScope: 'SimilarProduct', items: mockItems })

      render(
        <MemoryRouter>
          <RecommendationShelfLoader productId="prod-1" />
        </MemoryRouter>,
      )

      await waitFor(() => {
        expect(screen.getByText('Sản phẩm tương tự')).toBeInTheDocument()
      })
      expect(getSimilar).toHaveBeenCalledWith(
        'prod-1',
        expect.objectContaining({ limit: 8 }),
      )
    })

    it('shows error state when the request fails', async () => {
      vi.spyOn(recommendationApi, 'getRecommendations').mockRejectedValue(new Error('boom'))

      render(<RecommendationShelfLoader />)

      await waitFor(() => {
        expect(screen.getByText('Thử lại')).toBeInTheDocument()
      })
    })
  })
})