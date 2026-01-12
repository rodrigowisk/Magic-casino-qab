import apiSports from './apiSports';

export default {
  async getMyBets() {
    // Chama o endpoint que criamos no BetsController
    return apiSports.get('/bets/my-history');
  }
};