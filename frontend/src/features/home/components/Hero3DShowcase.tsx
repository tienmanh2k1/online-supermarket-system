import { useEffect, useRef, useState, useCallback } from 'react'
import * as THREE from 'three'

export interface Hero3DShowcaseProps {
  className?: string
  autoRotateSpeed?: number
}

export function Hero3DShowcase({
  className = '',
  autoRotateSpeed = 0.005,
}: Hero3DShowcaseProps) {
  const containerRef = useRef<HTMLDivElement>(null)
  const canvasRef = useRef<HTMLCanvasElement>(null)
  const fallbackRef = useRef<HTMLDivElement>(null)
  const [webglActive, setWebglActive] = useState<boolean>(false)
  const [isDragging, setIsDragging] = useState<boolean>(false)

  // Use refs instead of state to prevent 60fps React re-renders
  const rotationRef = useRef<{ x: number; y: number }>({ x: 6, y: 25 })
  const draggingRef = useRef<boolean>(false)
  const frameRef = useRef<number | null>(null)
  const visibleRef = useRef<{ intersecting: boolean; docVisible: boolean }>({
    intersecting: true,
    docVisible: typeof document !== 'undefined' ? !document.hidden : true,
  })

  const dragStartRef = useRef<{ x: number; y: number; rotY: number; rotX: number }>({
    x: 0,
    y: 0,
    rotY: 25,
    rotX: 6,
  })

  // Pointer drag handlers for 3D rotation
  const handlePointerDown = useCallback((e: React.PointerEvent) => {
    draggingRef.current = true
    setIsDragging(true)
    dragStartRef.current = {
      x: e.clientX,
      y: e.clientY,
      rotY: rotationRef.current.y,
      rotX: rotationRef.current.x,
    }
    ;(e.target as HTMLElement).setPointerCapture?.(e.pointerId)
  }, [])

  const handlePointerMove = useCallback((e: React.PointerEvent) => {
    if (!draggingRef.current) return
    const deltaX = e.clientX - dragStartRef.current.x
    const deltaY = e.clientY - dragStartRef.current.y

    const nextRotY = Math.max(-45, Math.min(45, dragStartRef.current.rotY + deltaX * 0.35))
    const nextRotX = Math.max(-14, Math.min(16, dragStartRef.current.rotX - deltaY * 0.2))

    rotationRef.current.y = nextRotY
    rotationRef.current.x = nextRotX

    if (fallbackRef.current) {
      fallbackRef.current.style.transform = `perspective(900px) rotateX(${nextRotX}deg) rotateY(${nextRotY}deg)`
    }
  }, [])

  const handlePointerUp = useCallback(() => {
    draggingRef.current = false
    setIsDragging(false)
  }, [])

  // WebGL and Animation Lifecycle (stable single effect with [] dependencies)
  useEffect(() => {
    const canvas = canvasRef.current
    const container = containerRef.current
    if (!canvas || !container) return

    let gl: WebGLRenderingContext | WebGL2RenderingContext | null = null
    try {
      gl =
        (canvas.getContext('webgl2') as WebGL2RenderingContext | null) ||
        (canvas.getContext('webgl') as WebGLRenderingContext | null) ||
        (canvas.getContext('experimental-webgl') as WebGLRenderingContext | null)
    } catch {
      gl = null
    }

    if (!gl) {
      // Graceful fallback to static CSS 3D Hologram stage without idle RAF loop
      setWebglActive(false)
      // Placeholder RAF so cancelAnimationFrame in cleanup remains predictable
      frameRef.current = requestAnimationFrame(() => {})
      return () => {
        if (frameRef.current !== null) {
          cancelAnimationFrame(frameRef.current)
          frameRef.current = null
        }
      }
    }

    let isDisposed = false
    let renderer: THREE.WebGLRenderer

    try {
      renderer = new THREE.WebGLRenderer({
        canvas,
        context: gl,
        alpha: true,
        antialias: true,
        powerPreference: 'high-performance',
      })
      const width = container.clientWidth || 480
      const height = container.clientHeight || 320
      renderer.setSize(width, height)
      renderer.setPixelRatio(Math.min(typeof window !== 'undefined' ? window.devicePixelRatio : 1, 2))
      setWebglActive(true)
    } catch {
      setWebglActive(false)
      return
    }

    const width = container.clientWidth || 480
    const height = container.clientHeight || 320
    const scene = new THREE.Scene()
    const camera = new THREE.PerspectiveCamera(40, width / height, 0.1, 100)
    camera.position.set(0, 0.3, 4.2)

    // Lighting setup
    const ambientLight = new THREE.AmbientLight(0x0f172a, 1.5)
    scene.add(ambientLight)

    const keyLight = new THREE.DirectionalLight(0xffffff, 2.5)
    keyLight.position.set(3, 4, 3)
    scene.add(keyLight)

    const fillLight = new THREE.PointLight(0x00f0ff, 3, 10)
    fillLight.position.set(-3, -0.5, 2.5)
    scene.add(fillLight)

    const rimLight = new THREE.DirectionalLight(0x818cf8, 2.8)
    rimLight.position.set(-2, 3, -3)
    scene.add(rimLight)

    // Showroom Stage Group
    const stageGroup = new THREE.Group()
    scene.add(stageGroup)

    // Cyber Pedestal
    const pedestalGeo = new THREE.CylinderGeometry(1.6, 1.75, 0.12, 48)
    const pedestalMat = new THREE.MeshStandardMaterial({
      color: 0x0f172a,
      roughness: 0.2,
      metalness: 0.85,
    })
    const pedestal = new THREE.Mesh(pedestalGeo, pedestalMat)
    pedestal.position.y = -0.75
    stageGroup.add(pedestal)

    // Neon Glow Ring
    const ringGeo1 = new THREE.TorusGeometry(1.62, 0.02, 16, 64)
    const ringMat1 = new THREE.MeshBasicMaterial({ color: 0x00f2fe })
    const ring1 = new THREE.Mesh(ringGeo1, ringMat1)
    ring1.rotation.x = Math.PI / 2
    ring1.position.y = -0.68
    stageGroup.add(ring1)

    // TV Screen Frame
    const tvGroup = new THREE.Group()
    tvGroup.position.y = 0.05
    stageGroup.add(tvGroup)

    const frameGeo = new THREE.BoxGeometry(2.3, 1.35, 0.04)
    const frameMat = new THREE.MeshStandardMaterial({
      color: 0x0284c7,
      roughness: 0.1,
      metalness: 0.9,
    })
    const screenFrame = new THREE.Mesh(frameGeo, frameMat)
    tvGroup.add(screenFrame)

    // Display face with glowing emissive picture
    const displayGeo = new THREE.PlaneGeometry(2.24, 1.29)
    const displayMat = new THREE.MeshStandardMaterial({
      color: 0x38bdf8,
      emissive: 0x0284c7,
      emissiveIntensity: 0.6,
      roughness: 0.1,
    })
    const display = new THREE.Mesh(displayGeo, displayMat)
    display.position.z = 0.022
    tvGroup.add(display)

    // TV Stand & Soundbar
    const standNeckGeo = new THREE.CylinderGeometry(0.04, 0.05, 0.35, 16)
    const standNeckMat = new THREE.MeshStandardMaterial({ color: 0xe2e8f0, metalness: 0.9 })
    const standNeck = new THREE.Mesh(standNeckGeo, standNeckMat)
    standNeck.position.set(0, -0.75, 0)
    tvGroup.add(standNeck)

    const soundbarGeo = new THREE.BoxGeometry(1.6, 0.08, 0.12)
    const soundbarMat = new THREE.MeshStandardMaterial({ color: 0x1e293b, metalness: 0.8 })
    const soundbar = new THREE.Mesh(soundbarGeo, soundbarMat)
    soundbar.position.set(0, -0.62, 0.25)
    stageGroup.add(soundbar)

    // Floating Particles
    const particleCount = 80
    const particleGeo = new THREE.BufferGeometry()
    const positions = new Float32Array(particleCount * 3)
    for (let i = 0; i < particleCount * 3; i += 3) {
      positions[i] = (Math.random() - 0.5) * 6
      positions[i + 1] = (Math.random() - 0.5) * 4 + 0.2
      positions[i + 2] = (Math.random() - 0.5) * 4
    }
    particleGeo.setAttribute('position', new THREE.BufferAttribute(positions, 3))
    const particleMat = new THREE.PointsMaterial({
      color: 0x38bdf8,
      size: 0.035,
      transparent: true,
      opacity: 0.7,
      blending: THREE.AdditiveBlending,
    })
    const particles = new THREE.Points(particleGeo, particleMat)
    scene.add(particles)

    // Visibility gates: IntersectionObserver & Document visibilitychange
    const handleVisibilityChange = () => {
      visibleRef.current.docVisible = !document.hidden
    }
    document.addEventListener('visibilitychange', handleVisibilityChange)

    let observer: IntersectionObserver | null = null
    if (typeof IntersectionObserver !== 'undefined') {
      observer = new IntersectionObserver(([entry]) => {
        visibleRef.current.intersecting = entry.isIntersecting
      })
      observer.observe(container)
    }

    const reduceMotion =
      typeof window !== 'undefined' &&
      window.matchMedia &&
      window.matchMedia('(prefers-reduced-motion: reduce)').matches

    const clock = new THREE.Clock()

    const animate = () => {
      if (isDisposed) return
      frameRef.current = requestAnimationFrame(animate)

      const isVisible = visibleRef.current.intersecting && visibleRef.current.docVisible
      if (!isVisible) return

      const elapsed = clock.getElapsedTime()

      // Idle sway when not dragging and reduced motion is off
      if (!reduceMotion && !draggingRef.current) {
        rotationRef.current.y = Math.sin(elapsed * 0.75) * 20
        rotationRef.current.x = Math.cos(elapsed * 0.45) * 4
      }

      stageGroup.rotation.y = (rotationRef.current.y * Math.PI) / 180
      stageGroup.rotation.x = (rotationRef.current.x * Math.PI) / 180
      ring1.rotation.z = elapsed * 0.6
      particles.rotation.y = elapsed * 0.05

      renderer.render(scene, camera)
    }

    frameRef.current = requestAnimationFrame(animate)

    return () => {
      isDisposed = true
      if (frameRef.current !== null) {
        cancelAnimationFrame(frameRef.current)
        frameRef.current = null
      }
      document.removeEventListener('visibilitychange', handleVisibilityChange)
      observer?.disconnect()

      pedestalGeo.dispose()
      pedestalMat.dispose()
      ringGeo1.dispose()
      ringMat1.dispose()
      frameGeo.dispose()
      frameMat.dispose()
      displayGeo.dispose()
      displayMat.dispose()
      standNeckGeo.dispose()
      standNeckMat.dispose()
      soundbarGeo.dispose()
      soundbarMat.dispose()
      particleGeo.dispose()
      particleMat.dispose()
      renderer.dispose()
      renderer.forceContextLoss()
    }
  }, [])

  return (
    <div
      ref={containerRef}
      className={`hero-3d-showcase ${isDragging ? 'is-interacting' : ''} ${className}`}
      data-testid="hero-3d-showcase"
      onPointerDown={handlePointerDown}
      onPointerMove={handlePointerMove}
      onPointerUp={handlePointerUp}
      onPointerCancel={handlePointerUp}
    >
      {/* 1. WebGL Canvas Stage */}
      <canvas
        ref={canvasRef}
        className="hero-3d-canvas"
        aria-hidden="true"
        style={{ display: webglActive ? 'block' : 'none' }}
      />

      {/* 2. Interactive 3D Cyber Hologram Stage (Fallback when WebGL is unavailable) */}
      {!webglActive && (
        <div
          ref={fallbackRef}
          className="hero-3d-hologram-stage"
          data-testid="hero-3d-fallback"
          style={{
            transform: `perspective(900px) rotateX(${rotationRef.current.x}deg) rotateY(${rotationRef.current.y}deg)`,
          }}
        >
          {/* Cyber Pedestal Base with Glowing Neon Ring */}
          <div className="hologram-pedestal">
            <div className="hologram-ring hologram-ring--outer" />
            <div className="hologram-ring hologram-ring--inner" />
            <div className="hologram-laser-glow" />
          </div>

          {/* 3D Neo QLED TV Showcase Entity */}
          <div className="hologram-tv-model">
            {/* TV Bezel & Screen Face */}
            <div className="hologram-tv-screen">
              <div className="hologram-screen-content">
                <span className="hologram-brand-badge">SAMSUNG NEO QLED 8K</span>
                <div className="hologram-visual-wave" />
                <span className="hologram-spec-tag">NQ8 AI Gen3 • 144Hz OLED Pro</span>
              </div>
              <div className="hologram-bezel-reflection" />
            </div>

            {/* Premium Metallic Soundbar */}
            <div className="hologram-soundbar">
              <span className="hologram-soundbar-led" />
              <small>DOLBY ATMOS 3D SPATIAL</small>
            </div>
          </div>

          {/* Orbiting 3D Spec Badges */}
          <div className="hologram-orbit-badge hologram-orbit-badge--left">
            <span className="hologram-orbit-icon">⚡</span>
            <span>AI Upscaling 8K</span>
          </div>
          <div className="hologram-orbit-badge hologram-orbit-badge--right">
            <span className="hologram-orbit-icon">🎮</span>
            <span>0.1ms 144Hz VRR</span>
          </div>
        </div>
      )}

      {/* Floating Interactive 360 Affordance Bar */}
      <div className="hero-3d-hint" aria-hidden="true">
        <span className="hero-3d-hint__icon">↺</span>
        <span className="hero-3d-hint__text">
          {isDragging ? 'Đang xoay 3D...' : 'Kéo chuột để xoay 360° Showroom'}
        </span>
      </div>
    </div>
  )
}
