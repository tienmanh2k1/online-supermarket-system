export const TEST_PREFIX = process.env.UI_TEST_PREFIX || 'QA_UI_20260910_';

export const ACCOUNTS = {
  ADMIN: {
    email: 'admin@test.com',
    password: 'Test@123',
    role: 'Admin',
    name: 'Admin User',
  },
  CUSTOMER_1: {
    email: `${TEST_PREFIX}c1@test.com`,
    password: 'Password@123',
    name: 'Nguyen Van QA C1',
    phone: '0912345001',
  },
  CUSTOMER_2: {
    email: `${TEST_PREFIX}c2@test.com`,
    password: 'Password@123',
    name: 'Tran Thi QA C2',
    phone: '0912345002',
  },
  CUSTOMER_3: {
    email: `${TEST_PREFIX}c3@test.com`,
    password: 'Password@123',
    name: 'Le Hoang QA C3',
    phone: '0912345003',
  },
};

export const ARITHMETIC_TRUTH_TABLE = {
  DELIVERY_SHIPPING_FEE: 15000,
  PICKUP_SHIPPING_FEE: 0,
  
  // Independent truth functions:
  calcPickup: (unitPrice: number, quantity: number) => {
    const subtotal = unitPrice * quantity;
    const shipping = 0;
    const total = subtotal + shipping;
    return { subtotal, shipping, total };
  },

  calcDelivery: (unitPrice: number, quantity: number, couponDiscount = 0) => {
    const subtotal = unitPrice * quantity;
    const shipping = 15000;
    const effectiveDiscount = Math.min(subtotal, couponDiscount);
    const total = subtotal - effectiveDiscount + shipping;
    return { subtotal, shipping, discount: effectiveDiscount, total };
  },

  calcPercentageCoupon: (subtotal: number, percent: number) => {
    return Math.round((subtotal * percent) / 100);
  },

  calcStockTransition: (onHand: number, reserved: number, deltaReserved: number, deltaOnHand: number) => {
    const nextOnHand = onHand + deltaOnHand;
    const nextReserved = reserved + deltaReserved;
    const nextAvailable = nextOnHand - nextReserved;
    return { onHand: nextOnHand, reserved: nextReserved, available: nextAvailable };
  },
};
