
import "vue-tsx-support/enable-check"   

import Vue from "vue";
import VueRouter from "vue-router";
import RouterPrefetch from 'vue-router-prefetch'
import App from "./App";
// TIP: change to import router from "./router/starterRouter"; to start with a clean layout
import router from "./router/index";

import i18n from "./i18n"
import './registerServiceWorker'


import 'vuetify/dist/vuetify.min.css'
import "./assets/less/core.less";   

Vue.use(VueRouter);
Vue.use(RouterPrefetch);

/* eslint-disable no-new */
new Vue({
    router,
    i18n,
    render: h => h(App)
}).$mount("#app");
