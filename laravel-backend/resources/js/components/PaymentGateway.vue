<template>
    <div class="payment-gateway">
        <!-- Header -->
        <div class="gateway-header">
            <h2>
                <i class="fas fa-mobile-alt"></i>
                SMS Payment Gateway
            </h2>
            <div class="mobile-status" :class="mobileStatusClass">
                <span class="status-indicator"></span>
                {{ mobileStatusText }}
            </div>
        </div>

        <!-- Stats Cards -->
        <div class="stats-grid">
            <div class="stat-card pending">
                <div class="stat-icon">
                    <i class="fas fa-clock"></i>
                </div>
                <div class="stat-info">
                    <span class="stat-value">{{ stats.pendingPayments }}</span>
                    <span class="stat-label">รอตรวจสอบ</span>
                </div>
            </div>
            <div class="stat-card approved">
                <div class="stat-icon">
                    <i class="fas fa-check-circle"></i>
                </div>
                <div class="stat-info">
                    <span class="stat-value">{{ stats.approvedPayments }}</span>
                    <span class="stat-label">อนุมัติแล้ว</span>
                </div>
            </div>
            <div class="stat-card total">
                <div class="stat-icon">
                    <i class="fas fa-baht-sign"></i>
                </div>
                <div class="stat-info">
                    <span class="stat-value">฿{{ formatNumber(stats.totalAmountApproved) }}</span>
                    <span class="stat-label">ยอดวันนี้</span>
                </div>
            </div>
            <div class="stat-card devices">
                <div class="stat-icon">
                    <i class="fas fa-mobile-screen"></i>
                </div>
                <div class="stat-info">
                    <span class="stat-value">{{ connectedDevices.length }}</span>
                    <span class="stat-label">อุปกรณ์เชื่อมต่อ</span>
                </div>
            </div>
        </div>

        <!-- Connected Devices -->
        <div class="section-card">
            <div class="section-header">
                <h3>
                    <i class="fas fa-plug"></i>
                    อุปกรณ์ที่เชื่อมต่อ
                </h3>
                <button class="btn-refresh" @click="loadDevices">
                    <i class="fas fa-sync-alt" :class="{ spinning: loading }"></i>
                </button>
            </div>
            <div class="devices-list" v-if="connectedDevices.length > 0">
                <div
                    v-for="device in connectedDevices"
                    :key="device.deviceId"
                    class="device-card"
                    :class="{ online: device.isOnline }"
                >
                    <div class="device-icon">
                        <i class="fas fa-mobile-alt"></i>
                    </div>
                    <div class="device-info">
                        <div class="device-name">{{ device.deviceName }}</div>
                        <div class="device-details">
                            <span class="platform">{{ device.platform }}</span>
                            <span class="separator">•</span>
                            <span class="version">v{{ device.appVersion }}</span>
                        </div>
                    </div>
                    <div class="device-status">
                        <div class="sync-status" :class="device.syncStatus">
                            <i :class="getSyncIcon(device.syncStatus)"></i>
                            {{ getSyncText(device.syncStatus) }}
                        </div>
                        <div class="device-stats">
                            <span v-if="device.batteryLevel">
                                <i class="fas fa-battery-three-quarters"></i>
                                {{ device.batteryLevel }}%
                            </span>
                            <span v-if="device.networkType">
                                <i :class="device.networkType === 'WiFi' ? 'fas fa-wifi' : 'fas fa-signal'"></i>
                            </span>
                        </div>
                    </div>
                    <div class="device-payments">
                        <div class="pending-badge" v-if="device.pendingPayments > 0">
                            {{ device.pendingPayments }} รอตรวจสอบ
                        </div>
                        <div class="today-amount">
                            ฿{{ formatNumber(device.totalPaymentsToday) }}
                        </div>
                    </div>
                </div>
            </div>
            <div class="empty-state" v-else>
                <i class="fas fa-mobile-alt"></i>
                <p>ยังไม่มีอุปกรณ์เชื่อมต่อ</p>
                <small>เปิดแอพ AI Manager Mobile บนมือถือของคุณ</small>
            </div>
        </div>

        <!-- Payments List -->
        <div class="section-card">
            <div class="section-header">
                <h3>
                    <i class="fas fa-credit-card"></i>
                    รายการชำระเงิน
                </h3>
                <div class="filter-buttons">
                    <button
                        v-for="filter in filters"
                        :key="filter.value"
                        class="filter-btn"
                        :class="{ active: currentFilter === filter.value }"
                        @click="currentFilter = filter.value"
                    >
                        {{ filter.label }}
                    </button>
                </div>
            </div>

            <div class="payments-table" v-if="filteredPayments.length > 0">
                <table>
                    <thead>
                        <tr>
                            <th>เวลา</th>
                            <th>ธนาคาร</th>
                            <th>จำนวน</th>
                            <th>Confidence</th>
                            <th>สถานะ</th>
                            <th>การดำเนินการ</th>
                        </tr>
                    </thead>
                    <tbody>
                        <tr v-for="payment in filteredPayments" :key="payment.id">
                            <td>
                                <div class="time-cell">
                                    {{ formatTime(payment.transactionTime) }}
                                    <small>{{ formatDate(payment.transactionTime) }}</small>
                                </div>
                            </td>
                            <td>
                                <div class="bank-cell">
                                    <span class="bank-icon" :style="{ background: getBankColor(payment.bankName) }">
                                        {{ payment.bankName.charAt(0) }}
                                    </span>
                                    <div>
                                        <div>{{ payment.bankName }}</div>
                                        <small>{{ payment.accountNumber }}</small>
                                    </div>
                                </div>
                            </td>
                            <td>
                                <span class="amount" :class="payment.type">
                                    {{ payment.type === 'incoming' ? '+' : '-' }}฿{{ formatNumber(payment.amount) }}
                                </span>
                            </td>
                            <td>
                                <div class="confidence-bar">
                                    <div
                                        class="confidence-fill"
                                        :style="{ width: (payment.confidenceScore * 100) + '%' }"
                                        :class="getConfidenceClass(payment.confidenceScore)"
                                    ></div>
                                    <span>{{ Math.round(payment.confidenceScore * 100) }}%</span>
                                </div>
                            </td>
                            <td>
                                <span class="status-badge" :class="payment.status.toLowerCase()">
                                    {{ getStatusText(payment.status) }}
                                </span>
                            </td>
                            <td>
                                <div class="actions">
                                    <button
                                        v-if="payment.status === 'Pending' || payment.status === 'Verified'"
                                        class="btn-approve"
                                        @click="approvePayment(payment)"
                                        :disabled="processing"
                                    >
                                        <i class="fas fa-check"></i>
                                    </button>
                                    <button
                                        v-if="payment.status === 'Pending' || payment.status === 'Verified'"
                                        class="btn-reject"
                                        @click="rejectPayment(payment)"
                                        :disabled="processing"
                                    >
                                        <i class="fas fa-times"></i>
                                    </button>
                                    <button
                                        class="btn-view"
                                        @click="viewPaymentDetails(payment)"
                                    >
                                        <i class="fas fa-eye"></i>
                                    </button>
                                </div>
                            </td>
                        </tr>
                    </tbody>
                </table>
            </div>
            <div class="empty-state" v-else>
                <i class="fas fa-inbox"></i>
                <p>ไม่มีรายการชำระเงิน</p>
            </div>
        </div>

        <!-- Pending Orders -->
        <div class="section-card">
            <div class="section-header">
                <h3>
                    <i class="fas fa-shopping-cart"></i>
                    คำสั่งซื้อรอชำระเงิน
                </h3>
                <button class="btn-create-order" @click="showCreateOrderModal = true">
                    <i class="fas fa-plus"></i>
                    สร้างคำสั่งซื้อ
                </button>
            </div>

            <!-- Unique Amount Info -->
            <div class="unique-amount-info">
                <i class="fas fa-info-circle"></i>
                <span>
                    ระบบจะเพิ่มสตางค์อัตโนมัติเพื่อให้ยอดไม่ซ้ำกัน ทำให้จับคู่ SMS กับ Order ได้แม่นยำ 100%
                </span>
            </div>

            <div class="orders-grid" v-if="pendingOrders.length > 0">
                <div v-for="order in pendingOrders" :key="order.id" class="order-card">
                    <div class="order-header">
                        <span class="order-number">{{ order.order_number }}</span>
                        <span class="order-status" :class="order.status">
                            {{ getOrderStatusText(order.status) }}
                        </span>
                    </div>

                    <!-- แสดงยอดที่ต้องชำระ -->
                    <div class="order-amount-section">
                        <div class="order-amount">{{ order.amount_formatted || '฿' + formatNumber(order.amount) }}</div>
                        <div class="amount-breakdown" v-if="order.has_suffix || order.amount_suffix > 0">
                            <span class="base-amount">ยอดเดิม ฿{{ formatNumber(order.base_amount) }}</span>
                            <span class="suffix-badge">
                                <i class="fas fa-plus-circle"></i>
                                {{ Math.round(order.amount_suffix * 100) }} สตางค์
                            </span>
                        </div>
                    </div>

                    <div class="order-description" v-if="order.description">{{ order.description }}</div>

                    <!-- Customer Info -->
                    <div class="order-customer" v-if="order.customer_name">
                        <i class="fas fa-user"></i>
                        {{ order.customer_name }}
                    </div>

                    <div class="order-timer" v-if="order.status === 'pending'">
                        <i class="fas fa-clock"></i>
                        หมดเวลาใน {{ getTimeRemaining(order.expires_at) }}
                    </div>

                    <div class="order-actions" v-if="order.status === 'pending'">
                        <button class="btn-copy-amount" @click="copyAmount(order)" title="คัดลอกยอดเงิน">
                            <i class="fas fa-copy"></i>
                            คัดลอกยอด
                        </button>
                        <button class="btn-match" @click="showMatchModal(order)">
                            <i class="fas fa-link"></i>
                            จับคู่
                        </button>
                    </div>
                </div>
            </div>
            <div class="empty-state" v-else>
                <i class="fas fa-shopping-cart"></i>
                <p>ไม่มีคำสั่งซื้อที่รอชำระเงิน</p>
            </div>
        </div>

        <!-- Payment Details Modal -->
        <div class="modal-overlay" v-if="selectedPayment" @click.self="selectedPayment = null">
            <div class="modal-content">
                <div class="modal-header">
                    <h3>รายละเอียดการชำระเงิน</h3>
                    <button class="modal-close" @click="selectedPayment = null">
                        <i class="fas fa-times"></i>
                    </button>
                </div>
                <div class="modal-body">
                    <div class="detail-grid">
                        <div class="detail-item">
                            <label>ธนาคาร</label>
                            <span>{{ selectedPayment.bankName }}</span>
                        </div>
                        <div class="detail-item">
                            <label>บัญชี</label>
                            <span>{{ selectedPayment.accountNumber }}</span>
                        </div>
                        <div class="detail-item">
                            <label>จำนวนเงิน</label>
                            <span class="amount-lg">฿{{ formatNumber(selectedPayment.amount) }}</span>
                        </div>
                        <div class="detail-item">
                            <label>เวลา</label>
                            <span>{{ formatDateTime(selectedPayment.transactionTime) }}</span>
                        </div>
                        <div class="detail-item">
                            <label>อ้างอิง</label>
                            <span>{{ selectedPayment.reference || '-' }}</span>
                        </div>
                        <div class="detail-item">
                            <label>Confidence</label>
                            <span>{{ Math.round(selectedPayment.confidenceScore * 100) }}%</span>
                        </div>
                    </div>
                    <div class="raw-message">
                        <label>ข้อความ SMS ต้นฉบับ</label>
                        <pre>{{ selectedPayment.rawMessage }}</pre>
                    </div>
                </div>
            </div>
        </div>

        <!-- Create Order Modal -->
        <div class="modal-overlay" v-if="showCreateOrderModal" @click.self="showCreateOrderModal = false">
            <div class="modal-content">
                <div class="modal-header">
                    <h3>สร้างคำสั่งซื้อใหม่</h3>
                    <button class="modal-close" @click="showCreateOrderModal = false">
                        <i class="fas fa-times"></i>
                    </button>
                </div>
                <div class="modal-body">
                    <div class="form-group">
                        <label>จำนวนเงิน (บาท)</label>
                        <input type="number" v-model="newOrder.amount" placeholder="0.00" step="0.01" />
                    </div>
                    <div class="form-group">
                        <label>รายละเอียด</label>
                        <input type="text" v-model="newOrder.description" placeholder="รายละเอียดคำสั่งซื้อ" />
                    </div>
                    <div class="form-group">
                        <label>ชื่อลูกค้า (ไม่บังคับ)</label>
                        <input type="text" v-model="newOrder.customer_name" placeholder="ชื่อลูกค้า" />
                    </div>
                    <div class="form-group">
                        <label>หมดเวลาใน (นาที)</label>
                        <input type="number" v-model="newOrder.expires_in" placeholder="30" />
                    </div>

                    <!-- Unique Amount Notice -->
                    <div class="unique-amount-notice" v-if="newOrder.amount > 0">
                        <i class="fas fa-magic"></i>
                        <div>
                            <strong>ระบบจะปรับยอดอัตโนมัติ</strong>
                            <p>หากมี Order อื่นที่ยอดใกล้เคียงกัน ระบบจะเพิ่มสตางค์เล็กน้อยเพื่อให้จับคู่ SMS ได้แม่นยำ</p>
                        </div>
                    </div>
                </div>
                <div class="modal-footer">
                    <button class="btn-cancel" @click="showCreateOrderModal = false">ยกเลิก</button>
                    <button class="btn-primary" @click="createOrder" :disabled="processing || !newOrder.amount">
                        <i class="fas fa-plus"></i>
                        สร้างคำสั่งซื้อ
                    </button>
                </div>
            </div>
        </div>

        <!-- Order Created Success Modal -->
        <div class="modal-overlay" v-if="createdOrder" @click.self="createdOrder = null">
            <div class="modal-content success-modal">
                <div class="modal-header success">
                    <h3><i class="fas fa-check-circle"></i> สร้าง Order สำเร็จ</h3>
                    <button class="modal-close" @click="createdOrder = null">
                        <i class="fas fa-times"></i>
                    </button>
                </div>
                <div class="modal-body">
                    <div class="order-success-info">
                        <div class="order-number-large">{{ createdOrder.order_number }}</div>
                        <div class="amount-to-pay">
                            <label>ยอดที่ต้องชำระ</label>
                            <div class="amount-value">{{ createdOrder.payment_instructions?.amount_formatted }}</div>
                        </div>

                        <div class="amount-notice" v-if="createdOrder.amount_notice">
                            <i class="fas fa-info-circle"></i>
                            {{ createdOrder.amount_notice }}
                        </div>

                        <div class="payment-instruction-box">
                            <p>{{ createdOrder.payment_instructions?.reason }}</p>
                        </div>
                    </div>
                </div>
                <div class="modal-footer">
                    <button class="btn-copy-lg" @click="copyOrderAmount(createdOrder)">
                        <i class="fas fa-copy"></i>
                        คัดลอกยอดเงิน
                    </button>
                    <button class="btn-primary" @click="createdOrder = null">
                        ตกลง
                    </button>
                </div>
            </div>
        </div>
    </div>
</template>

<script>
import { ref, computed, onMounted, onUnmounted } from 'vue';
import axios from 'axios';

export default {
    name: 'PaymentGateway',

    setup() {
        const loading = ref(false);
        const processing = ref(false);
        const payments = ref([]);
        const connectedDevices = ref([]);
        const pendingOrders = ref([]);
        const stats = ref({
            pendingPayments: 0,
            approvedPayments: 0,
            totalAmountApproved: 0
        });
        const currentFilter = ref('all');
        const selectedPayment = ref(null);
        const showCreateOrderModal = ref(false);
        const createdOrder = ref(null);
        const newOrder = ref({
            amount: null,
            description: '',
            customer_name: '',
            expires_in: 30
        });

        const filters = [
            { label: 'ทั้งหมด', value: 'all' },
            { label: 'รอตรวจสอบ', value: 'pending' },
            { label: 'อนุมัติแล้ว', value: 'approved' },
            { label: 'ปฏิเสธ', value: 'rejected' }
        ];

        const mobileStatusClass = computed(() => {
            if (connectedDevices.value.length === 0) return 'offline';
            return connectedDevices.value.some(d => d.isOnline) ? 'online' : 'offline';
        });

        const mobileStatusText = computed(() => {
            const online = connectedDevices.value.filter(d => d.isOnline).length;
            if (online === 0) return 'ไม่มีอุปกรณ์เชื่อมต่อ';
            return `${online} อุปกรณ์ออนไลน์`;
        });

        const filteredPayments = computed(() => {
            if (currentFilter.value === 'all') return payments.value;
            return payments.value.filter(p =>
                p.status.toLowerCase() === currentFilter.value ||
                (currentFilter.value === 'pending' && p.status === 'Verified')
            );
        });

        const aiManagerApiUrl = import.meta.env.VITE_AI_MANAGER_API_URL || 'http://localhost:5000';

        const loadPayments = async () => {
            try {
                loading.value = true;
                const response = await axios.get(`${aiManagerApiUrl}/api/paymentgateway/payments`);
                if (response.data.success) {
                    payments.value = response.data.data;
                }
            } catch (error) {
                console.error('Failed to load payments:', error);
            } finally {
                loading.value = false;
            }
        };

        const loadDevices = async () => {
            try {
                const response = await axios.get(`${aiManagerApiUrl}/api/mobileconnection/devices`);
                if (response.data.success) {
                    connectedDevices.value = response.data.data;
                }
            } catch (error) {
                console.error('Failed to load devices:', error);
            }
        };

        const loadStats = async () => {
            try {
                const response = await axios.get(`${aiManagerApiUrl}/api/paymentgateway/stats`);
                if (response.data.success) {
                    stats.value = response.data.data;
                }
            } catch (error) {
                console.error('Failed to load stats:', error);
            }
        };

        const loadOrders = async () => {
            try {
                const response = await axios.get(`${aiManagerApiUrl}/api/paymentgateway/orders`, {
                    params: { status: 'Pending' }
                });
                if (response.data.success) {
                    pendingOrders.value = response.data.data;
                }
            } catch (error) {
                console.error('Failed to load orders:', error);
            }
        };

        const approvePayment = async (payment) => {
            try {
                processing.value = true;
                const response = await axios.post(
                    `${aiManagerApiUrl}/api/paymentgateway/${payment.id}/approve`
                );
                if (response.data.success) {
                    payment.status = 'Approved';
                    await loadStats();
                }
            } catch (error) {
                console.error('Failed to approve payment:', error);
            } finally {
                processing.value = false;
            }
        };

        const rejectPayment = async (payment) => {
            const reason = prompt('ระบุเหตุผลในการปฏิเสธ:');
            if (!reason) return;

            try {
                processing.value = true;
                const response = await axios.post(
                    `${aiManagerApiUrl}/api/paymentgateway/${payment.id}/reject`,
                    { rejectedBy: 'admin', reason }
                );
                if (response.data.success) {
                    payment.status = 'Rejected';
                    await loadStats();
                }
            } catch (error) {
                console.error('Failed to reject payment:', error);
            } finally {
                processing.value = false;
            }
        };

        const viewPaymentDetails = (payment) => {
            selectedPayment.value = payment;
        };

        const createOrder = async () => {
            if (!newOrder.value.amount) return;

            try {
                processing.value = true;
                // ใช้ Laravel API แทน AI Manager API
                const response = await axios.post('/api/v1/payment-gateway/orders', {
                    amount: parseFloat(newOrder.value.amount),
                    description: newOrder.value.description || 'ไม่ระบุรายละเอียด',
                    customer_name: newOrder.value.customer_name || null,
                    expires_in: parseInt(newOrder.value.expires_in) || 30
                });

                if (response.data.success) {
                    showCreateOrderModal.value = false;

                    // แสดง modal แจ้งผลสำเร็จพร้อมยอดที่ต้องชำระ
                    createdOrder.value = {
                        ...response.data.data,
                        amount_notice: response.data.amount_notice,
                        payment_instructions: response.data.payment_instructions
                    };

                    // Reset form
                    newOrder.value = { amount: null, description: '', customer_name: '', expires_in: 30 };

                    await loadOrders();
                }
            } catch (error) {
                console.error('Failed to create order:', error);
                alert('ไม่สามารถสร้าง Order ได้: ' + (error.response?.data?.message || error.message));
            } finally {
                processing.value = false;
            }
        };

        const copyAmount = async (order) => {
            try {
                await navigator.clipboard.writeText(order.amount.toString());
                alert(`คัดลอกยอด ${order.amount} บาท แล้ว`);
            } catch (err) {
                console.error('Failed to copy:', err);
            }
        };

        const copyOrderAmount = async (order) => {
            try {
                const amount = order.payment_instructions?.amount_to_pay || order.amount;
                await navigator.clipboard.writeText(amount.toString());
                alert(`คัดลอกยอด ${amount} บาท แล้ว`);
            } catch (err) {
                console.error('Failed to copy:', err);
            }
        };

        const showMatchModal = (order) => {
            // TODO: Implement match modal
            alert(`จับคู่ Order ${order.order_number} - ยอด ${order.amount} บาท`);
        };

        // Utility functions
        const formatNumber = (num) => {
            return new Intl.NumberFormat('th-TH').format(num || 0);
        };

        const formatTime = (date) => {
            return new Date(date).toLocaleTimeString('th-TH', { hour: '2-digit', minute: '2-digit' });
        };

        const formatDate = (date) => {
            return new Date(date).toLocaleDateString('th-TH', { day: '2-digit', month: 'short' });
        };

        const formatDateTime = (date) => {
            return new Date(date).toLocaleString('th-TH');
        };

        const getStatusText = (status) => {
            const texts = {
                'Pending': 'รอตรวจสอบ',
                'Verified': 'ตรวจสอบแล้ว',
                'Matched': 'จับคู่แล้ว',
                'Approved': 'อนุมัติแล้ว',
                'Rejected': 'ปฏิเสธ'
            };
            return texts[status] || status;
        };

        const getOrderStatusText = (status) => {
            const texts = {
                'Pending': 'รอชำระเงิน',
                'Matched': 'จับคู่แล้ว',
                'Paid': 'ชำระแล้ว',
                'Expired': 'หมดเวลา'
            };
            return texts[status] || status;
        };

        const getSyncIcon = (status) => {
            const icons = {
                'synced': 'fas fa-cloud',
                'syncing': 'fas fa-sync-alt spinning',
                'disconnected': 'fas fa-cloud-slash',
                'offline': 'fas fa-cloud-slash'
            };
            return icons[status] || 'fas fa-question';
        };

        const getSyncText = (status) => {
            const texts = {
                'synced': 'Synced',
                'syncing': 'Syncing...',
                'disconnected': 'Disconnected',
                'offline': 'Offline'
            };
            return texts[status] || status;
        };

        const getConfidenceClass = (score) => {
            if (score >= 0.9) return 'high';
            if (score >= 0.7) return 'medium';
            return 'low';
        };

        const getBankColor = (bankName) => {
            const colors = {
                'กสิกรไทย': '#138f2d',
                'ไทยพาณิชย์': '#4e2a82',
                'กรุงเทพ': '#1e4598',
                'กรุงไทย': '#1ba5e0',
                'ทหารไทยธนชาต': '#004c9f',
                'กรุงศรีอยุธยา': '#fec40c',
                'ออมสิน': '#ed1c24',
                'ธกส': '#00703c',
                'พร้อมเพย์': '#032383'
            };
            return colors[bankName] || '#666';
        };

        const getTimeRemaining = (expiresAt) => {
            const now = new Date();
            const expires = new Date(expiresAt);
            const diff = expires - now;
            if (diff <= 0) return 'หมดเวลาแล้ว';
            const minutes = Math.floor(diff / 60000);
            const seconds = Math.floor((diff % 60000) / 1000);
            return `${minutes}:${seconds.toString().padStart(2, '0')}`;
        };

        // Refresh interval
        let refreshInterval;
        onMounted(() => {
            loadPayments();
            loadDevices();
            loadStats();
            loadOrders();

            refreshInterval = setInterval(() => {
                loadDevices();
                loadStats();
            }, 15000);
        });

        onUnmounted(() => {
            if (refreshInterval) clearInterval(refreshInterval);
        });

        return {
            loading,
            processing,
            payments,
            connectedDevices,
            pendingOrders,
            stats,
            currentFilter,
            filters,
            selectedPayment,
            showCreateOrderModal,
            createdOrder,
            newOrder,
            mobileStatusClass,
            mobileStatusText,
            filteredPayments,
            loadPayments,
            loadDevices,
            approvePayment,
            rejectPayment,
            viewPaymentDetails,
            createOrder,
            copyAmount,
            copyOrderAmount,
            showMatchModal,
            formatNumber,
            formatTime,
            formatDate,
            formatDateTime,
            getStatusText,
            getOrderStatusText,
            getSyncIcon,
            getSyncText,
            getConfidenceClass,
            getBankColor,
            getTimeRemaining
        };
    }
};
</script>

<style scoped>
.payment-gateway {
    padding: 24px;
    background: var(--dark-bg, #0D1117);
    min-height: 100vh;
    color: #fff;
}

.gateway-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 24px;
}

.gateway-header h2 {
    display: flex;
    align-items: center;
    gap: 12px;
    font-size: 24px;
    color: #fff;
}

.gateway-header h2 i {
    color: var(--primary, #7C4DFF);
}

.mobile-status {
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 8px 16px;
    border-radius: 20px;
    font-size: 14px;
}

.mobile-status.online {
    background: rgba(76, 175, 80, 0.2);
    color: #4CAF50;
}

.mobile-status.offline {
    background: rgba(158, 158, 158, 0.2);
    color: #9E9E9E;
}

.status-indicator {
    width: 8px;
    height: 8px;
    border-radius: 50%;
    background: currentColor;
}

/* Stats Grid */
.stats-grid {
    display: grid;
    grid-template-columns: repeat(4, 1fr);
    gap: 16px;
    margin-bottom: 24px;
}

.stat-card {
    background: var(--card-bg, #1E1E2E);
    border-radius: 12px;
    padding: 20px;
    display: flex;
    align-items: center;
    gap: 16px;
}

.stat-icon {
    width: 48px;
    height: 48px;
    border-radius: 12px;
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 20px;
}

.stat-card.pending .stat-icon {
    background: rgba(255, 152, 0, 0.2);
    color: #FF9800;
}

.stat-card.approved .stat-icon {
    background: rgba(76, 175, 80, 0.2);
    color: #4CAF50;
}

.stat-card.total .stat-icon {
    background: rgba(124, 77, 255, 0.2);
    color: #7C4DFF;
}

.stat-card.devices .stat-icon {
    background: rgba(0, 188, 212, 0.2);
    color: #00BCD4;
}

.stat-value {
    font-size: 24px;
    font-weight: bold;
    display: block;
}

.stat-label {
    font-size: 12px;
    color: var(--muted, #9090A8);
}

/* Section Cards */
.section-card {
    background: var(--card-bg, #1E1E2E);
    border-radius: 12px;
    padding: 20px;
    margin-bottom: 24px;
}

.section-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 16px;
}

.section-header h3 {
    display: flex;
    align-items: center;
    gap: 8px;
    font-size: 16px;
    color: #fff;
}

.section-header h3 i {
    color: var(--primary, #7C4DFF);
}

/* Devices List */
.devices-list {
    display: flex;
    flex-direction: column;
    gap: 12px;
}

.device-card {
    display: grid;
    grid-template-columns: auto 1fr auto auto;
    gap: 16px;
    align-items: center;
    padding: 16px;
    background: var(--card-light, #2A2A3E);
    border-radius: 8px;
    border-left: 3px solid #9E9E9E;
}

.device-card.online {
    border-left-color: #4CAF50;
}

.device-icon {
    width: 40px;
    height: 40px;
    border-radius: 8px;
    background: rgba(124, 77, 255, 0.2);
    display: flex;
    align-items: center;
    justify-content: center;
    color: var(--primary, #7C4DFF);
}

.device-name {
    font-weight: 600;
}

.device-details {
    font-size: 12px;
    color: var(--muted, #9090A8);
}

.sync-status {
    display: flex;
    align-items: center;
    gap: 6px;
    font-size: 12px;
    padding: 4px 8px;
    border-radius: 4px;
}

.sync-status.synced {
    background: rgba(76, 175, 80, 0.2);
    color: #4CAF50;
}

.sync-status.syncing {
    background: rgba(0, 188, 212, 0.2);
    color: #00BCD4;
}

.sync-status.disconnected,
.sync-status.offline {
    background: rgba(158, 158, 158, 0.2);
    color: #9E9E9E;
}

.device-stats {
    display: flex;
    gap: 12px;
    font-size: 12px;
    color: var(--muted, #9090A8);
    margin-top: 4px;
}

.pending-badge {
    background: rgba(255, 152, 0, 0.2);
    color: #FF9800;
    padding: 4px 8px;
    border-radius: 4px;
    font-size: 12px;
}

.today-amount {
    font-size: 14px;
    font-weight: 600;
    color: #4CAF50;
}

/* Payments Table */
.payments-table {
    overflow-x: auto;
}

.payments-table table {
    width: 100%;
    border-collapse: collapse;
}

.payments-table th,
.payments-table td {
    padding: 12px;
    text-align: left;
    border-bottom: 1px solid var(--card-light, #2A2A3E);
}

.payments-table th {
    color: var(--muted, #9090A8);
    font-weight: 500;
    font-size: 12px;
    text-transform: uppercase;
}

.time-cell small {
    display: block;
    color: var(--muted, #9090A8);
    font-size: 11px;
}

.bank-cell {
    display: flex;
    align-items: center;
    gap: 12px;
}

.bank-icon {
    width: 32px;
    height: 32px;
    border-radius: 6px;
    display: flex;
    align-items: center;
    justify-content: center;
    color: #fff;
    font-weight: bold;
    font-size: 14px;
}

.amount {
    font-weight: 600;
}

.amount.incoming {
    color: #4CAF50;
}

.amount.outgoing {
    color: #F44336;
}

.confidence-bar {
    position: relative;
    height: 20px;
    background: var(--card-light, #2A2A3E);
    border-radius: 10px;
    overflow: hidden;
    min-width: 80px;
}

.confidence-fill {
    position: absolute;
    top: 0;
    left: 0;
    height: 100%;
    border-radius: 10px;
}

.confidence-fill.high {
    background: rgba(76, 175, 80, 0.5);
}

.confidence-fill.medium {
    background: rgba(255, 152, 0, 0.5);
}

.confidence-fill.low {
    background: rgba(244, 67, 54, 0.5);
}

.confidence-bar span {
    position: absolute;
    width: 100%;
    text-align: center;
    font-size: 11px;
    line-height: 20px;
}

.status-badge {
    padding: 4px 12px;
    border-radius: 12px;
    font-size: 12px;
    font-weight: 500;
}

.status-badge.pending,
.status-badge.verified {
    background: rgba(255, 152, 0, 0.2);
    color: #FF9800;
}

.status-badge.matched {
    background: rgba(33, 150, 243, 0.2);
    color: #2196F3;
}

.status-badge.approved {
    background: rgba(76, 175, 80, 0.2);
    color: #4CAF50;
}

.status-badge.rejected {
    background: rgba(244, 67, 54, 0.2);
    color: #F44336;
}

.actions {
    display: flex;
    gap: 8px;
}

.actions button {
    width: 32px;
    height: 32px;
    border-radius: 6px;
    border: none;
    cursor: pointer;
    display: flex;
    align-items: center;
    justify-content: center;
}

.btn-approve {
    background: rgba(76, 175, 80, 0.2);
    color: #4CAF50;
}

.btn-reject {
    background: rgba(244, 67, 54, 0.2);
    color: #F44336;
}

.btn-view {
    background: rgba(124, 77, 255, 0.2);
    color: #7C4DFF;
}

/* Filter Buttons */
.filter-buttons {
    display: flex;
    gap: 8px;
}

.filter-btn {
    padding: 6px 16px;
    border-radius: 6px;
    border: none;
    background: var(--card-light, #2A2A3E);
    color: var(--muted, #9090A8);
    cursor: pointer;
    font-size: 13px;
}

.filter-btn.active {
    background: var(--primary, #7C4DFF);
    color: #fff;
}

/* Orders Grid */
.orders-grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
    gap: 16px;
}

.order-card {
    background: var(--card-light, #2A2A3E);
    border-radius: 8px;
    padding: 16px;
}

.order-header {
    display: flex;
    justify-content: space-between;
    margin-bottom: 12px;
}

.order-number {
    font-size: 12px;
    color: var(--muted, #9090A8);
}

.order-status {
    font-size: 12px;
    padding: 2px 8px;
    border-radius: 4px;
}

.order-status.pending {
    background: rgba(255, 152, 0, 0.2);
    color: #FF9800;
}

.order-status.paid {
    background: rgba(76, 175, 80, 0.2);
    color: #4CAF50;
}

.order-amount {
    font-size: 24px;
    font-weight: bold;
    margin-bottom: 8px;
}

.order-description {
    font-size: 13px;
    color: var(--muted, #9090A8);
    margin-bottom: 12px;
}

.order-timer {
    display: flex;
    align-items: center;
    gap: 6px;
    font-size: 12px;
    color: #FF9800;
}

.order-actions {
    margin-top: 12px;
}

.btn-match {
    width: 100%;
    padding: 8px;
    border-radius: 6px;
    border: 1px dashed var(--primary, #7C4DFF);
    background: transparent;
    color: var(--primary, #7C4DFF);
    cursor: pointer;
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 8px;
}

/* Buttons */
.btn-refresh {
    background: transparent;
    border: none;
    color: var(--muted, #9090A8);
    cursor: pointer;
    padding: 8px;
}

.btn-create-order {
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 8px 16px;
    border-radius: 6px;
    border: none;
    background: var(--primary, #7C4DFF);
    color: #fff;
    cursor: pointer;
}

/* Modal */
.modal-overlay {
    position: fixed;
    top: 0;
    left: 0;
    right: 0;
    bottom: 0;
    background: rgba(0, 0, 0, 0.7);
    display: flex;
    align-items: center;
    justify-content: center;
    z-index: 1000;
}

.modal-content {
    background: var(--card-bg, #1E1E2E);
    border-radius: 12px;
    width: 90%;
    max-width: 500px;
    max-height: 80vh;
    overflow-y: auto;
}

.modal-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 20px;
    border-bottom: 1px solid var(--card-light, #2A2A3E);
}

.modal-close {
    background: transparent;
    border: none;
    color: var(--muted, #9090A8);
    cursor: pointer;
    padding: 8px;
}

.modal-body {
    padding: 20px;
}

.modal-footer {
    display: flex;
    justify-content: flex-end;
    gap: 12px;
    padding: 20px;
    border-top: 1px solid var(--card-light, #2A2A3E);
}

.detail-grid {
    display: grid;
    grid-template-columns: repeat(2, 1fr);
    gap: 16px;
}

.detail-item label {
    display: block;
    font-size: 12px;
    color: var(--muted, #9090A8);
    margin-bottom: 4px;
}

.detail-item span {
    font-size: 14px;
}

.amount-lg {
    font-size: 24px !important;
    font-weight: bold;
    color: #4CAF50;
}

.raw-message {
    margin-top: 20px;
}

.raw-message label {
    display: block;
    font-size: 12px;
    color: var(--muted, #9090A8);
    margin-bottom: 8px;
}

.raw-message pre {
    background: var(--card-light, #2A2A3E);
    padding: 12px;
    border-radius: 8px;
    font-size: 12px;
    white-space: pre-wrap;
    word-break: break-all;
}

.form-group {
    margin-bottom: 16px;
}

.form-group label {
    display: block;
    font-size: 12px;
    color: var(--muted, #9090A8);
    margin-bottom: 8px;
}

.form-group input {
    width: 100%;
    padding: 12px;
    border-radius: 8px;
    border: 1px solid var(--card-light, #2A2A3E);
    background: var(--card-light, #2A2A3E);
    color: #fff;
    font-size: 14px;
}

.btn-cancel {
    padding: 10px 20px;
    border-radius: 6px;
    border: 1px solid var(--card-light, #2A2A3E);
    background: transparent;
    color: var(--muted, #9090A8);
    cursor: pointer;
}

.btn-primary {
    padding: 10px 20px;
    border-radius: 6px;
    border: none;
    background: var(--primary, #7C4DFF);
    color: #fff;
    cursor: pointer;
    display: flex;
    align-items: center;
    gap: 8px;
}

/* Empty State */
.empty-state {
    text-align: center;
    padding: 40px;
    color: var(--muted, #9090A8);
}

.empty-state i {
    font-size: 48px;
    margin-bottom: 16px;
    opacity: 0.5;
}

.empty-state p {
    margin: 0;
}

.empty-state small {
    display: block;
    margin-top: 8px;
    font-size: 12px;
}

/* Unique Amount Styles */
.unique-amount-info {
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 12px 16px;
    background: rgba(124, 77, 255, 0.1);
    border: 1px solid rgba(124, 77, 255, 0.3);
    border-radius: 8px;
    margin-bottom: 16px;
    font-size: 13px;
    color: var(--primary, #7C4DFF);
}

.unique-amount-info i {
    font-size: 16px;
}

.order-amount-section {
    margin-bottom: 12px;
}

.amount-breakdown {
    display: flex;
    align-items: center;
    gap: 8px;
    margin-top: 4px;
    font-size: 12px;
}

.base-amount {
    color: var(--muted, #9090A8);
}

.suffix-badge {
    display: inline-flex;
    align-items: center;
    gap: 4px;
    padding: 2px 8px;
    background: rgba(76, 175, 80, 0.2);
    color: #4CAF50;
    border-radius: 4px;
    font-size: 11px;
}

.suffix-badge i {
    font-size: 10px;
}

.order-customer {
    display: flex;
    align-items: center;
    gap: 6px;
    font-size: 13px;
    color: var(--muted, #9090A8);
    margin-bottom: 8px;
}

.order-actions {
    display: flex;
    gap: 8px;
    margin-top: 12px;
}

.btn-copy-amount {
    flex: 1;
    padding: 8px;
    border-radius: 6px;
    border: 1px solid var(--card-light, #2A2A3E);
    background: transparent;
    color: var(--muted, #9090A8);
    cursor: pointer;
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 6px;
    font-size: 12px;
}

.btn-copy-amount:hover {
    background: var(--card-light, #2A2A3E);
}

.unique-amount-notice {
    display: flex;
    align-items: flex-start;
    gap: 12px;
    padding: 12px;
    background: rgba(255, 193, 7, 0.1);
    border: 1px solid rgba(255, 193, 7, 0.3);
    border-radius: 8px;
    margin-top: 16px;
}

.unique-amount-notice i {
    color: #FFC107;
    font-size: 20px;
    margin-top: 2px;
}

.unique-amount-notice strong {
    display: block;
    color: #FFC107;
    margin-bottom: 4px;
}

.unique-amount-notice p {
    margin: 0;
    font-size: 12px;
    color: var(--muted, #9090A8);
}

/* Success Modal */
.success-modal .modal-header.success {
    background: rgba(76, 175, 80, 0.1);
}

.success-modal .modal-header.success h3 {
    color: #4CAF50;
    display: flex;
    align-items: center;
    gap: 8px;
}

.order-success-info {
    text-align: center;
}

.order-number-large {
    font-size: 14px;
    color: var(--muted, #9090A8);
    margin-bottom: 16px;
}

.amount-to-pay {
    margin-bottom: 16px;
}

.amount-to-pay label {
    display: block;
    font-size: 12px;
    color: var(--muted, #9090A8);
    margin-bottom: 8px;
}

.amount-value {
    font-size: 36px;
    font-weight: bold;
    color: #4CAF50;
}

.amount-notice {
    display: flex;
    align-items: flex-start;
    gap: 8px;
    padding: 12px;
    background: rgba(33, 150, 243, 0.1);
    border-radius: 8px;
    margin-bottom: 16px;
    text-align: left;
    font-size: 13px;
    color: #2196F3;
}

.amount-notice i {
    margin-top: 2px;
}

.payment-instruction-box {
    padding: 12px;
    background: var(--card-light, #2A2A3E);
    border-radius: 8px;
}

.payment-instruction-box p {
    margin: 0;
    font-size: 13px;
    color: var(--muted, #9090A8);
}

.btn-copy-lg {
    padding: 10px 20px;
    border-radius: 6px;
    border: 1px solid var(--primary, #7C4DFF);
    background: transparent;
    color: var(--primary, #7C4DFF);
    cursor: pointer;
    display: flex;
    align-items: center;
    gap: 8px;
}

.btn-copy-lg:hover {
    background: rgba(124, 77, 255, 0.1);
}

/* Spinning animation */
.spinning {
    animation: spin 1s linear infinite;
}

@keyframes spin {
    from { transform: rotate(0deg); }
    to { transform: rotate(360deg); }
}

/* Responsive */
@media (max-width: 768px) {
    .stats-grid {
        grid-template-columns: repeat(2, 1fr);
    }

    .device-card {
        grid-template-columns: 1fr;
    }

    .detail-grid {
        grid-template-columns: 1fr;
    }
}
</style>
