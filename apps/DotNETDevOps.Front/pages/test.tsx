
import * as tsx from "vue-tsx-support";
import { Component, Prop, Watch } from 'vue-property-decorator';

import { VBtn } from "vuetify-tsx"

export interface FrontPageOptions {

}
@Component
export default class FrontPage extends tsx.Component<FrontPageOptions>{






  render() {

    return (
      <div>Hello World</div>
    );
  }
}
