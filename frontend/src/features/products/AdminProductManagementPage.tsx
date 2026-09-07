import React, { useEffect, useState } from 'react'
import { catalogApi, type ProductSummaryDto, type CategoryDto, type BrandDto } from '../../api/catalogApi'

export function AdminProductManagementPage() {
  const [products, setProducts] = useState<ProductSummaryDto[]>([])
  const [categories, setCategories] = useState<CategoryDto[]>([])
  const [brands, setBrands] = useState<BrandDto[]>([])
  const [loading, setLoading] = useState(true)
  const [showModal, setShowModal] = useState(false)

  const [form, setForm] = useState({
    name: '',
    sku: '',
    basePrice: 0,
    unit: 'cái',
    categoryId: '',
    brandId: '',
    description: '',
    imageUrl: ''
  })

  const loadData = async () => {
    setLoading(true)
    try {
      const [prodRes, cats, brs] = await Promise.all([
        catalogApi.getProducts({ page: 1, pageSize: 50 }),
        catalogApi.getCategories(),
        catalogApi.getBrands()
      ])
      setProducts(prodRes.data)
      setCategories(cats)
      setBrands(brs)
      if (cats.length > 0) setForm(f => ({ ...f, categoryId: cats[0].id }))
      if (brs.length > 0) setForm(f => ({ ...f, brandId: brs[0].id }))
    } catch {
      alert('Không thể tải dữ liệu quản trị sản phẩm.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { loadData() }, [])

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    try {
      await catalogApi.createProduct(form)
      alert('Thêm sản phẩm thành công!')
      setShowModal(false)
      loadData()
    } catch {
      alert('Lỗi khi thêm sản phẩm.')
    }
  }

  const handleDelete = async (id: string) => {
    if (confirm('Bạn có chắc muốn xóa sản phẩm này?')) {
      await catalogApi.deleteProduct(id)
      loadData()
    }
  }

  return (
    <div style={{ padding: '24px', maxWidth: '1200px', margin: '0 auto' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '20px' }}>
        <h2>Quản Lý Sản Phẩm (Admin)</h2>
        <button 
          onClick={() => setShowModal(true)}
          style={{ padding: '10px 16px', background: '#047857', color: '#fff', border: 'none', borderRadius: '6px', cursor: 'pointer' }}
        >
          + Thêm Sản Phẩm Mới
        </button>
      </div>

      {loading ? <p>Đang tải danh sách...</p> : (
        <table style={{ width: '100%', borderCollapse: 'collapse', border: '1px solid #e5e7eb' }}>
          <thead>
            <tr style={{ background: '#f3f4f6', textAlign: 'left' }}>
              <th style={{ padding: '12px' }}>Tên Sản Phẩm</th>
              <th style={{ padding: '12px' }}>Mã SKU</th>
              <th style={{ padding: '12px' }}>Danh Mục</th>
              <th style={{ padding: '12px' }}>Thương Hiệu</th>
              <th style={{ padding: '12px' }}>Giá Gốc</th>
              <th style={{ padding: '12px' }}>Hành Động</th>
            </tr>
          </thead>
          <tbody>
            {products.map(p => (
              <tr key={p.id} style={{ borderBottom: '1px solid #e5e7eb' }}>
                <td style={{ padding: '12px' }}><strong>{p.name}</strong></td>
                <td style={{ padding: '12px' }}>{p.sku}</td>
                <td style={{ padding: '12px' }}>{p.categoryName}</td>
                <td style={{ padding: '12px' }}>{p.brandName}</td>
                <td style={{ padding: '12px' }}>{p.basePrice.toLocaleString('vi-VN')} đ</td>
                <td style={{ padding: '12px' }}>
                  <button onClick={() => handleDelete(p.id)} style={{ color: '#dc2626', background: 'none', border: 'none', cursor: 'pointer' }}>Xóa</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {showModal && (
        <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
          <div style={{ background: '#fff', padding: '24px', borderRadius: '8px', width: '500px' }}>
            <h3>Thêm Sản Phẩm Mới</h3>
            <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '12px', marginTop: '16px' }}>
              <input placeholder="Tên sản phẩm" value={form.name} onChange={e => setForm({...form, name: e.target.value})} required style={{ padding: '8px' }} />
              <input placeholder="Mã SKU" value={form.sku} onChange={e => setForm({...form, sku: e.target.value})} required style={{ padding: '8px' }} />
              <input type="number" placeholder="Giá gốc" value={form.basePrice} onChange={e => setForm({...form, basePrice: parseFloat(e.target.value) || 0})} required style={{ padding: '8px' }} />
              <select value={form.categoryId} onChange={e => setForm({...form, categoryId: e.target.value})} style={{ padding: '8px' }}>
                {categories.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
              </select>
              <select value={form.brandId} onChange={e => setForm({...form, brandId: e.target.value})} style={{ padding: '8px' }}>
                {brands.map(b => <option key={b.id} value={b.id}>{b.name}</option>)}
              </select>
              <input placeholder="Đơn vị (VD: hộp, kg)" value={form.unit} onChange={e => setForm({...form, unit: e.target.value})} style={{ padding: '8px' }} />
              <input placeholder="Link ảnh (URL)" value={form.imageUrl} onChange={e => setForm({...form, imageUrl: e.target.value})} style={{ padding: '8px' }} />
              <textarea placeholder="Mô tả sản phẩm" value={form.description} onChange={e => setForm({...form, description: e.target.value})} style={{ padding: '8px' }} />
              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px', marginTop: '12px' }}>
                <button type="button" onClick={() => setShowModal(false)} style={{ padding: '8px 16px' }}>Hủy</button>
                <button type="submit" style={{ padding: '8px 16px', background: '#047857', color: '#fff', border: 'none', borderRadius: '4px' }}>Lưu</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}
