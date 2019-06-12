
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

import Vuetify from 'vuetify'

Vue.use(Vuetify)

Vue.use(VueRouter);
Vue.use(RouterPrefetch);

declare module 'vue/types/vue' {
    export interface Vue {
        uniqId: string;
    }
}
Vue.use({
    install: function (Vue, options) {
        Object.defineProperty(Vue.prototype, "uniqId", {
            get: function uniqId() {
                return this._uid;
            }
        });
    }
});

/* eslint-disable no-new */
new Vue({
    router,
    i18n,
    render: h => h(App)
}).$mount("#app");
