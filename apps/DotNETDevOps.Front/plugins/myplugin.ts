


import Vue from "vue";


import "@/assets/less/core.less";


declare module 'vue/types/vue' {
    export interface Vue {
        uniqId: string;
    }
}
var c = 0;
//Vue.use({
//    install: function (Vue, options) {
       
         
//            console.log("TEST INstall")
//        if (!("uniqId" in Vue.prototype)) {
//            var n = c++;
//            Object.defineProperty(Vue.prototype, "uniqId", {
//                get: function uniqId() {
//                    return n;
//                },
//                configurable:true
//            });
//        }
//    }
//});
Vue.mixin({

    beforeCreate() {
        this.uniqId = "_" + c++;
    }
})
if (process.client) {
   
}

console.log("TEST")
